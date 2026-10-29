#define _POSIX_C_SOURCE 200809L

#include "transport_server_posix.h"
#include "transport_control_frontend.h"

#include <arpa/inet.h>
#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <sys/un.h>
#include <time.h>
#include <unistd.h>

#define CONTROL_DIRECTORY "/tmp/duck-cm4-transport"
#define CONTROL_SOCKET_PATH CONTROL_DIRECTORY "/control.sock"
#define MAX_CONTROL_LINE 4096
#define CONTROL_DEADLINE_MS 3000
#define UDP_PACKET_CAPACITY 2048
#define FEEDBACK_PORT_BASE 50100
#define FEEDBACK_PORT_COUNT 11
#define CM4_CONTROL_PORT 12346

typedef struct owned_socket_path {
    int bound;
    dev_t device;
    ino_t inode;
    uid_t owner;
} owned_socket_path;

typedef struct udp_drain_context {
    const int *descriptors;
    const struct timespec *deadline;
} udp_drain_context;

static volatile sig_atomic_t server_running = 1;

static void handle_signal(int signal_number)
{
    (void)signal_number;
    server_running = 0;
}

static int set_close_on_exec(int descriptor)
{
    int flags = fcntl(descriptor, F_GETFD);
    return flags >= 0 && fcntl(descriptor, F_SETFD, flags | FD_CLOEXEC) == 0;
}

static int remaining_milliseconds(const struct timespec *deadline)
{
    struct timespec now;
    long long milliseconds;
    if (clock_gettime(CLOCK_MONOTONIC, &now) != 0) return -1;
    milliseconds = (long long)(deadline->tv_sec - now.tv_sec) * 1000LL +
        (long long)(deadline->tv_nsec - now.tv_nsec) / 1000000LL;
    if (milliseconds <= 0) return 0;
    if (milliseconds > 3000) return 3000;
    return (int)milliseconds;
}

static int make_deadline(struct timespec *deadline)
{
    if (clock_gettime(CLOCK_MONOTONIC, deadline) != 0) return 0;
    deadline->tv_sec += CONTROL_DEADLINE_MS / 1000;
    deadline->tv_nsec += (long)(CONTROL_DEADLINE_MS % 1000) * 1000000L;
    if (deadline->tv_nsec >= 1000000000L) {
        ++deadline->tv_sec;
        deadline->tv_nsec -= 1000000000L;
    }
    return 1;
}

static int poll_until(int descriptor, short events, const struct timespec *deadline)
{
    struct pollfd poll_descriptor;
    int timeout = remaining_milliseconds(deadline);
    int result;
    if (timeout <= 0) return 0;
    poll_descriptor.fd = descriptor;
    poll_descriptor.events = events;
    poll_descriptor.revents = 0;
    do {
        result = poll(&poll_descriptor, 1, timeout);
    } while (result < 0 && errno == EINTR && server_running);
    if (result <= 0) return 0;
    return (poll_descriptor.revents & (events | POLLHUP)) != 0;
}

static int ensure_control_directory(int *created)
{
    struct stat status;
    *created = 0;
    if (mkdir(CONTROL_DIRECTORY, 0700) == 0) {
        *created = 1;
    } else if (errno != EEXIST) {
        return 0;
    }

    if (lstat(CONTROL_DIRECTORY, &status) != 0 || !S_ISDIR(status.st_mode) ||
        status.st_uid != geteuid() || (status.st_mode & 0777) != 0700) {
        return 0;
    }
    return 1;
}

static int bind_control_socket(int *created_directory, owned_socket_path *owned_path)
{
    struct sockaddr_un address;
    struct stat existing;
    struct stat bound;
    mode_t previous_mask;
    int descriptor;

    memset(owned_path, 0, sizeof(*owned_path));
    if (!ensure_control_directory(created_directory)) return -1;
    if (lstat(CONTROL_SOCKET_PATH, &existing) == 0 || errno != ENOENT) return -1;

    descriptor = socket(AF_UNIX, SOCK_STREAM, 0);
    if (descriptor < 0 || !set_close_on_exec(descriptor)) {
        if (descriptor >= 0) close(descriptor);
        return -1;
    }

    memset(&address, 0, sizeof(address));
    address.sun_family = AF_UNIX;
    if (strlen(CONTROL_SOCKET_PATH) >= sizeof(address.sun_path)) {
        close(descriptor);
        return -1;
    }
    memcpy(address.sun_path, CONTROL_SOCKET_PATH, strlen(CONTROL_SOCKET_PATH) + 1);

    previous_mask = umask(0077);
    if (bind(descriptor, (struct sockaddr *)&address, sizeof(address)) != 0) {
        umask(previous_mask);
        close(descriptor);
        return -1;
    }
    umask(previous_mask);

    if (lstat(CONTROL_SOCKET_PATH, &bound) != 0 || !S_ISSOCK(bound.st_mode) ||
        bound.st_uid != geteuid()) {
        close(descriptor);
        return -1;
    }
    owned_path->bound = 1;
    owned_path->device = bound.st_dev;
    owned_path->inode = bound.st_ino;
    owned_path->owner = bound.st_uid;
    if (chmod(CONTROL_SOCKET_PATH, 0600) != 0 || listen(descriptor, 4) != 0) {
        struct stat current;
        close(descriptor);
        if (lstat(CONTROL_SOCKET_PATH, &current) == 0 && S_ISSOCK(current.st_mode) &&
            current.st_dev == owned_path->device && current.st_ino == owned_path->inode &&
            current.st_uid == owned_path->owner) {
            (void)unlink(CONTROL_SOCKET_PATH);
        }
        owned_path->bound = 0;
        return -1;
    }

    {
        struct stat secured;
        if (lstat(CONTROL_SOCKET_PATH, &secured) != 0 || !S_ISSOCK(secured.st_mode) ||
            secured.st_dev != owned_path->device || secured.st_ino != owned_path->inode ||
            secured.st_uid != owned_path->owner || (secured.st_mode & 0777) != 0600) {
            close(descriptor);
            if (lstat(CONTROL_SOCKET_PATH, &secured) == 0 && S_ISSOCK(secured.st_mode) &&
                secured.st_dev == owned_path->device && secured.st_ino == owned_path->inode &&
                secured.st_uid == owned_path->owner) {
                (void)unlink(CONTROL_SOCKET_PATH);
            }
            owned_path->bound = 0;
            return -1;
        }
    }

    return descriptor;
}

static void cleanup_control_socket(int descriptor, const owned_socket_path *owned_path, int created_directory)
{
    struct stat current;
    if (descriptor >= 0) close(descriptor);
    if (owned_path != NULL && owned_path->bound && lstat(CONTROL_SOCKET_PATH, &current) == 0 &&
        S_ISSOCK(current.st_mode) && current.st_dev == owned_path->device &&
        current.st_ino == owned_path->inode && current.st_uid == owned_path->owner) {
        (void)unlink(CONTROL_SOCKET_PATH);
    }
    if (created_directory) (void)rmdir(CONTROL_DIRECTORY);
}

static int make_udp_socket(const uint8_t ipv4[4], unsigned int port)
{
    struct sockaddr_in address;
    int descriptor = socket(AF_INET, SOCK_DGRAM, 0);
    if (descriptor < 0 || !set_close_on_exec(descriptor)) {
        if (descriptor >= 0) close(descriptor);
        return -1;
    }

    memset(&address, 0, sizeof(address));
    address.sin_family = AF_INET;
    memcpy(&address.sin_addr.s_addr, ipv4, 4);
    address.sin_port = htons((uint16_t)port);
    if (bind(descriptor, (struct sockaddr *)&address, sizeof(address)) != 0) {
        close(descriptor);
        return -1;
    }
    return descriptor;
}

static int read_control_request(
    int descriptor,
    char *line,
    size_t capacity,
    size_t *length,
    int *received_any,
    const struct timespec *deadline)
{
    size_t used = 0;
    int saw_newline = 0;
    *received_any = 0;
    if (capacity < MAX_CONTROL_LINE + 1 || deadline == NULL) return 0;

    for (;;) {
        char buffer[512];
        ssize_t count;
        if (!poll_until(descriptor, POLLIN, deadline)) return 0;
        count = recv(descriptor, buffer, sizeof(buffer), 0);
        if (count < 0 && errno == EINTR) continue;
        if (count < 0) return 0;
        if (count == 0) break;
        *received_any = 1;
        for (ssize_t index = 0; index < count; ++index) {
            if (saw_newline || used >= MAX_CONTROL_LINE) return 0;
            if (buffer[index] == '\n') {
                saw_newline = 1;
            } else {
                line[used++] = buffer[index];
            }
        }
    }

    if (!saw_newline || used == 0) return 0;
    line[used] = '\0';
    *length = used;
    return 1;
}

static int send_control_ack(
    int descriptor,
    const char *ack,
    size_t length,
    const struct timespec *deadline)
{
    size_t sent = 0;
    char line[MAX_CONTROL_LINE + 1];
    if (length + 1 > sizeof(line) || deadline == NULL) return 0;
    memcpy(line, ack, length);
    line[length] = '\n';
    ++length;

    while (sent < length) {
        ssize_t count;
        if (!poll_until(descriptor, POLLOUT, deadline)) return 0;
        count = send(descriptor, line + sent, length - sent, MSG_NOSIGNAL);
        if (count < 0 && errno == EINTR) continue;
        if (count <= 0) return 0;
        sent += (size_t)count;
    }
    return 1;
}

static void clear_target(cm4_transport_state *state)
{
    state->target_valid = 0;
    memset(&state->simulator, 0, sizeof(state->simulator));
    memset(state->simulator_ipv4, 0, sizeof(state->simulator_ipv4));
}

static int drain_udp_receivers(void *context)
{
    const udp_drain_context *drain = (const udp_drain_context *)context;
    size_t drained = 0;

    if (drain == NULL || drain->descriptors == NULL || drain->deadline == NULL) return 0;
    for (;;) {
        size_t index;
        int all_empty = 1;
        if (!server_running || remaining_milliseconds(drain->deadline) <= 0) return 0;
        for (index = 0; index < FEEDBACK_PORT_COUNT + 1; ++index) {
            uint8_t packet[UDP_PACKET_CAPACITY];
            ssize_t count = recvfrom(drain->descriptors[index], packet, sizeof(packet),
                                     MSG_DONTWAIT | MSG_TRUNC, NULL, NULL);
            if (count < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)) continue;
            if (count < 0 && errno == EINTR) {
                all_empty = 0;
                continue;
            }
            if (count < 0 || (size_t)count > sizeof(packet) || ++drained > 4096) return 0;
            all_empty = 0;
        }
        if (all_empty) return server_running && remaining_milliseconds(drain->deadline) > 0;
    }
}

static void handle_control_connection(
    int listener,
    cm4_transport_state *state,
    const int *udp_receivers)
{
    char input[MAX_CONTROL_LINE + 1];
    char ack[MAX_CONTROL_LINE + 1];
    size_t input_length = 0;
    size_t ack_length = 0;
    cm4_transport_request request;
    struct timespec deadline;
    udp_drain_context drain;
    int received_any = 0;
    int client = accept(listener, NULL, NULL);
    if (client < 0) return;
    if (!set_close_on_exec(client)) {
        close(client);
        return;
    }
    if (!make_deadline(&deadline)) {
        close(client);
        return;
    }
    drain.descriptors = udp_receivers;
    drain.deadline = &deadline;

    if (!read_control_request(client, input, sizeof(input), &input_length, &received_any, &deadline)) {
        if (received_any) clear_target(state);
        close(client);
        return;
    }
    if (!cm4_transport_parse_request(input, input_length, &request)) {
        clear_target(state);
        close(client);
        return;
    }
    if (cm4_transport_apply_request_after_drain(
            state, &request, drain_udp_receivers, &drain) == CM4_TRANSPORT_APPLY_APPLIED) {
        if (!cm4_transport_write_ack(state, &request, ack, sizeof(ack), &ack_length) ||
            !send_control_ack(client, ack, ack_length, &deadline)) {
            clear_target(state);
        }
    }
    close(client);
}

static void forward_datagram(
    int receive_descriptor,
    int send_descriptor,
    const cm4_transport_state *state,
    int is_feedback,
    uint8_t robot_id)
{
    uint8_t packet[UDP_PACKET_CAPACITY];
    struct sockaddr_in source;
    struct sockaddr_in target;
    socklen_t source_length = sizeof(source);
    ssize_t received = recvfrom(receive_descriptor, packet, sizeof(packet), MSG_TRUNC,
                                (struct sockaddr *)&source, &source_length);
    cm4_transport_destination destination;
    if (received <= 0 || (size_t)received > sizeof(packet) ||
        source.sin_family != AF_INET) return;

    if (is_feedback) {
        const uint8_t *source_ipv4 = (const uint8_t *)&source.sin_addr.s_addr;
        if (!cm4_transport_route_simulator_feedback(state, source_ipv4, robot_id, &destination)) return;
    } else {
        const uint8_t *source_ipv4 = (const uint8_t *)&source.sin_addr.s_addr;
        if (!cm4_transport_route_cm4_outbound(state, source_ipv4, ntohs(source.sin_port), &destination)) return;
    }

    memset(&target, 0, sizeof(target));
    target.sin_family = AF_INET;
    memcpy(&target.sin_addr.s_addr, destination.ipv4, 4);
    target.sin_port = htons(destination.port);
    (void)sendto(send_descriptor, packet, (size_t)received, 0,
                 (struct sockaddr *)&target, sizeof(target));
}

int cm4_transport_run_server(
    const cm4_transport_identity *cm4,
    const char *helper_launch_id,
    const uint8_t cm4_ipv4[4])
{
    cm4_transport_state state;
    struct sigaction action;
    uint8_t loopback[4] = { 127, 0, 0, 1 };
    int feedback[FEEDBACK_PORT_COUNT];
    int udp_receivers[FEEDBACK_PORT_COUNT + 1];
    int cm4_outbound_receive = -1;
    int cm4_outbound_send = -1;
    int listener = -1;
    int created_directory = 0;
    owned_socket_path owned_path;
    struct pollfd descriptors[FEEDBACK_PORT_COUNT + 2];
    nfds_t descriptor_count;
    unsigned int index;
    int result = 1;

    for (index = 0; index < FEEDBACK_PORT_COUNT; ++index) feedback[index] = -1;
    memset(&owned_path, 0, sizeof(owned_path));
    if (cm4_ipv4 == NULL || !cm4_transport_state_init(&state, cm4, helper_launch_id)) return 2;
    if (cm4_ipv4[0] == 0 || cm4_ipv4[0] == 127 || cm4_ipv4[0] >= 224 ||
        (cm4_ipv4[0] == 169 && cm4_ipv4[1] == 254)) return 2;

    memset(&action, 0, sizeof(action));
    action.sa_handler = handle_signal;
    sigemptyset(&action.sa_mask);
    if (sigaction(SIGINT, &action, NULL) != 0 || sigaction(SIGTERM, &action, NULL) != 0) return 2;
    server_running = 1;

    listener = bind_control_socket(&created_directory, &owned_path);
    if (listener < 0) goto cleanup;
    cm4_outbound_receive = make_udp_socket(loopback, CM4_CONTROL_PORT);
    if (cm4_outbound_receive < 0) goto cleanup;
    udp_receivers[0] = cm4_outbound_receive;
    cm4_outbound_send = make_udp_socket(cm4_ipv4, CM4_CONTROL_PORT);
    if (cm4_outbound_send < 0) goto cleanup;
    for (index = 0; index < FEEDBACK_PORT_COUNT; ++index) {
        feedback[index] = make_udp_socket(cm4_ipv4, FEEDBACK_PORT_BASE + index);
        if (feedback[index] < 0) goto cleanup;
        udp_receivers[index + 1] = feedback[index];
    }

    descriptors[0].fd = listener;
    descriptors[0].events = POLLIN;
    descriptors[1].fd = cm4_outbound_receive;
    descriptors[1].events = POLLIN;
    for (index = 0; index < FEEDBACK_PORT_COUNT; ++index) {
        descriptors[index + 2].fd = feedback[index];
        descriptors[index + 2].events = POLLIN;
    }
    descriptor_count = FEEDBACK_PORT_COUNT + 2;
    result = 0;

    while (server_running) {
        int poll_result = poll(descriptors, descriptor_count, 100);
        if (poll_result < 0) {
            if (errno == EINTR) continue;
            result = 1;
            break;
        }
        if (poll_result == 0) continue;
        if (descriptors[0].revents & POLLIN) {
            handle_control_connection(listener, &state, udp_receivers);
        }
        if (descriptors[1].revents & POLLIN) {
            forward_datagram(cm4_outbound_receive, cm4_outbound_send, &state, 0, 0);
        }
        for (index = 0; index < FEEDBACK_PORT_COUNT; ++index) {
            if (descriptors[index + 2].revents & POLLIN) {
                forward_datagram(feedback[index], feedback[index], &state, 1, (uint8_t)index);
            }
        }
    }

cleanup:
    clear_target(&state);
    if (cm4_outbound_receive >= 0) close(cm4_outbound_receive);
    if (cm4_outbound_send >= 0) close(cm4_outbound_send);
    for (index = 0; index < FEEDBACK_PORT_COUNT; ++index) {
        if (feedback[index] >= 0) close(feedback[index]);
    }
    cleanup_control_socket(listener, &owned_path, created_directory);
    return result;
}

static int set_nonblocking(int descriptor)
{
    int flags = fcntl(descriptor, F_GETFL);
    return flags >= 0 && fcntl(descriptor, F_SETFL, flags | O_NONBLOCK) == 0;
}

static int write_all_until(
    int descriptor,
    const char *buffer,
    size_t length,
    const struct timespec *deadline)
{
    size_t sent = 0;
    while (sent < length) {
        ssize_t count;
        if (!poll_until(descriptor, POLLOUT, deadline)) return 0;
        count = send(descriptor, buffer + sent, length - sent, MSG_NOSIGNAL);
        if (count < 0 && (errno == EINTR || errno == EAGAIN || errno == EWOULDBLOCK)) continue;
        if (count <= 0) return 0;
        sent += (size_t)count;
    }
    return 1;
}

static int uds_exchange(
    void *context,
    const char *request,
    size_t request_length,
    char *response,
    size_t response_capacity,
    size_t *response_length)
{
    struct sockaddr_un address;
    struct timespec deadline;
    size_t used = 0;
    int saw_newline = 0;
    int descriptor = -1;
    int result = 0;
    (void)context;

    if (request == NULL || response == NULL || response_length == NULL ||
        response_capacity == 0 || !make_deadline(&deadline)) return 0;
    descriptor = socket(AF_UNIX, SOCK_STREAM, 0);
    if (descriptor < 0 || !set_close_on_exec(descriptor) || !set_nonblocking(descriptor)) goto done;
    memset(&address, 0, sizeof(address));
    address.sun_family = AF_UNIX;
    memcpy(address.sun_path, CONTROL_SOCKET_PATH, sizeof(CONTROL_SOCKET_PATH));
    if (connect(descriptor, (struct sockaddr *)&address, sizeof(address)) != 0) {
        int socket_error = 0;
        socklen_t socket_error_length = sizeof(socket_error);
        if (errno != EINPROGRESS || !poll_until(descriptor, POLLOUT, &deadline) ||
            getsockopt(descriptor, SOL_SOCKET, SO_ERROR, &socket_error, &socket_error_length) != 0 ||
            socket_error != 0) goto done;
    }
    if (!write_all_until(descriptor, request, request_length, &deadline) ||
        !write_all_until(descriptor, "\n", 1, &deadline) || shutdown(descriptor, SHUT_WR) != 0) goto done;

    for (;;) {
        char buffer[512];
        ssize_t count;
        if (!poll_until(descriptor, POLLIN, &deadline)) goto done;
        count = recv(descriptor, buffer, sizeof(buffer), 0);
        if (count < 0 && (errno == EINTR || errno == EAGAIN || errno == EWOULDBLOCK)) continue;
        if (count < 0) goto done;
        if (count == 0) break;
        for (ssize_t index = 0; index < count; ++index) {
            if (buffer[index] == '\n') {
                if (saw_newline) goto done;
                saw_newline = 1;
            } else {
                if (saw_newline || used >= response_capacity) goto done;
                response[used++] = buffer[index];
            }
        }
    }

    if (!saw_newline || used == 0) goto done;
    *response_length = used;
    result = 1;

done:
    if (descriptor >= 0) close(descriptor);
    return result;
}

int cm4_transport_run_control_cli(void)
{
    return cm4_transport_run_control_stdio(stdin, stdout, stderr, uds_exchange, NULL);
}
