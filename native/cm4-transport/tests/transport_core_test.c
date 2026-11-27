#include "../transport_core.h"
#include "../transport_control_frontend.h"
#include "../transport_path_policy.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define CHECK(condition, message) do { \
    if (!(condition)) { \
        fprintf(stderr, "assertion failed: %s\n", (message)); \
        return 1; \
    } \
} while (0)

typedef struct memory_exchange_state {
    cm4_transport_state target_state;
    int available;
    int malformed_ack;
    int calls;
} memory_exchange_state;

typedef struct drain_test_state {
    const cm4_transport_state *published_state;
    int calls;
    int result;
    int observed_invalidated_state;
} drain_test_state;

typedef struct poll_dispatch_test_state {
    int queued_datagrams;
    unsigned int control_events;
    unsigned int datagrams_processed;
} poll_dispatch_test_state;

typedef struct path_cleanup_test_state {
    unsigned int socket_unlinks;
    unsigned int directory_removals;
    uint32_t sentinel_mode;
    uint8_t sentinel_byte;
} path_cleanup_test_state;

static void test_unlink_owned_socket(void *context)
{
    path_cleanup_test_state *state = (path_cleanup_test_state *)context;
    ++state->socket_unlinks;
    state->sentinel_byte = 0;
}

static void test_remove_owned_directory(void *context)
{
    path_cleanup_test_state *state = (path_cleanup_test_state *)context;
    ++state->directory_removals;
    state->sentinel_mode = 0;
}

static void test_control_ready(void *context)
{
    poll_dispatch_test_state *state = (poll_dispatch_test_state *)context;
    ++state->control_events;
    state->queued_datagrams = 0;
}

static void test_datagram_ready(void *context, unsigned int index)
{
    poll_dispatch_test_state *state = (poll_dispatch_test_state *)context;
    (void)index;
    ++state->datagrams_processed;
    if (state->queued_datagrams > 0) --state->queued_datagrams;
}

static int test_drain_before_publish(void *context)
{
    drain_test_state *drain = (drain_test_state *)context;
    ++drain->calls;
    drain->observed_invalidated_state = !drain->published_state->target_valid &&
        drain->published_state->config_epoch == 2;
    return drain->result;
}

static void copy_literal(char *destination, size_t capacity, const char *source)
{
    size_t length = strlen(source);
    if (length >= capacity) {
        fputs("test fixture string exceeds its field\n", stderr);
        exit(2);
    }
    memcpy(destination, source, length + 1);
}

static int read_fixture(const char *path, char *buffer, size_t capacity, size_t *length)
{
    FILE *file = fopen(path, "rb");
    size_t count;
    if (file == NULL) {
        return 0;
    }

    count = fread(buffer, 1, capacity - 1, file);
    if (ferror(file) || !feof(file)) {
        fclose(file);
        return 0;
    }

    if (fclose(file) != 0) {
        return 0;
    }

    buffer[count] = '\0';
    while (count > 0 && (buffer[count - 1] == '\n' || buffer[count - 1] == '\r')) {
        buffer[--count] = '\0';
    }
    *length = count;
    return 1;
}

static int parse_rejected(const char *json, size_t length)
{
    cm4_transport_request request;
    memset(&request, 0x5a, sizeof(request));
    return !cm4_transport_parse_request(json, length, &request);
}

static int test_control_path_policy(void)
{
    const uint64_t owner = 42;
    cm4_transport_path_identity directory = { 1, 10, owner, 0700, CM4_TRANSPORT_PATH_DIRECTORY };
    cm4_transport_path_identity socket_path = { 1, 11, owner, 0600, CM4_TRANSPORT_PATH_SOCKET };
    cm4_transport_path_identity directory_after = directory;
    cm4_transport_path_identity socket_after = socket_path;
    cm4_transport_path_identity changed;
    path_cleanup_test_state cleanup = { 0, 0, 0640, 0x5a };

    CHECK(cm4_transport_socket_endpoint_matches(&directory, &directory_after,
                                                &socket_path, &socket_after, owner, owner),
          "accept the current same-owner private control socket");

    changed = directory;
    changed.kind = CM4_TRANSPORT_PATH_SYMLINK;
    CHECK(!cm4_transport_owned_directory_matches(&changed, owner),
          "reject a symlink in the control directory path");
    changed = directory;
    changed.owner++;
    CHECK(!cm4_transport_owned_directory_matches(&changed, owner),
          "reject a control directory owned by another user");
    changed = directory;
    changed.mode = 0777;
    CHECK(!cm4_transport_owned_directory_matches(&changed, owner),
          "reject an over-permissive control directory");

    changed = socket_path;
    changed.kind = CM4_TRANSPORT_PATH_SYMLINK;
    CHECK(!cm4_transport_owned_socket_matches(&changed, owner),
          "reject a symlink at the control socket path");
    changed = socket_path;
    changed.owner++;
    CHECK(!cm4_transport_owned_socket_matches(&changed, owner),
          "reject a control socket owned by another user");
    changed = socket_path;
    changed.mode = 0666;
    CHECK(!cm4_transport_owned_socket_matches(&changed, owner),
          "reject an over-permissive control socket");

    changed = directory_after;
    changed.inode++;
    CHECK(!cm4_transport_socket_endpoint_matches(&directory, &changed,
                                                &socket_path, &socket_after, owner, owner),
          "reject replacement of the control directory during connect");
    changed = socket_after;
    changed.inode++;
    CHECK(!cm4_transport_socket_endpoint_matches(&directory, &directory_after,
                                                &socket_path, &changed, owner, owner),
          "reject replacement of the control socket during connect");
    CHECK(!cm4_transport_socket_endpoint_matches(&directory, &directory_after,
                                                &socket_path, &socket_after, owner + 1, owner),
          "reject a connected peer with a different effective user");

    changed = socket_path;
    changed.kind = CM4_TRANSPORT_PATH_SYMLINK;
    CHECK(!cm4_transport_can_unlink_owned_socket(&socket_path, &changed),
          "do not unlink a replacement symlink");
    changed = socket_path;
    changed.inode++;
    CHECK(!cm4_transport_can_unlink_owned_socket(&socket_path, &changed),
          "do not unlink a replacement socket inode");
    CHECK(cm4_transport_can_unlink_owned_socket(&socket_path, &socket_after),
          "unlink only the recorded socket inode");

    changed = directory;
    changed.inode++;
    CHECK(!cm4_transport_can_remove_owned_directory(1, &directory, &changed),
          "do not remove a replacement control directory");
    CHECK(!cm4_transport_can_remove_owned_directory(0, &directory, &directory_after),
          "do not remove a directory not created by this process");
    CHECK(cm4_transport_can_remove_owned_directory(1, &directory, &directory_after),
          "remove the unchanged directory created by this process");

    changed = socket_path;
    changed.kind = CM4_TRANSPORT_PATH_SYMLINK;
    directory_after = directory;
    directory_after.inode++;
    cm4_transport_cleanup_owned_paths(1, 1, &socket_path, &changed,
                                      &directory, &directory_after,
                                      test_unlink_owned_socket, test_remove_owned_directory,
                                      &cleanup);
    CHECK(cleanup.socket_unlinks == 0 && cleanup.directory_removals == 0,
          "do not clean up replacement paths");
    CHECK(cleanup.sentinel_mode == 0640 && cleanup.sentinel_byte == 0x5a,
          "leave replacement sentinel mode and content unchanged");

    directory_after = directory;
    socket_after = socket_path;
    cm4_transport_cleanup_owned_paths(1, 1, &socket_path, &socket_after,
                                      &directory, &directory_after,
                                      test_unlink_owned_socket, test_remove_owned_directory,
                                      &cleanup);
    CHECK(cleanup.socket_unlinks == 1 && cleanup.directory_removals == 1,
          "clean up only the unchanged owned socket and directory");
    CHECK((0777u & ~cm4_transport_control_socket_umask()) == 0600u,
          "create the control socket privately without a later path chmod");
    CHECK(cm4_transport_control_socket_mode_is_private(0600u),
          "accept only the private socket creation mode");
    return 0;
}

static int memory_exchange(
    void *context,
    const char *request_json,
    size_t request_length,
    char *response,
    size_t response_capacity,
    size_t *response_length)
{
    memory_exchange_state *exchange = (memory_exchange_state *)context;
    cm4_transport_request request;
    ++exchange->calls;
    if (!exchange->available ||
        !cm4_transport_parse_request(request_json, request_length, &request)) {
        return 0;
    }
    if (strcmp(request.type, "invalidateTarget") == 0) {
        int count;
        if (!cm4_transport_invalidate(&exchange->target_state, request.config_epoch)) return 0;
        count = snprintf(response, response_capacity,
            "{\"version\":1,\"type\":\"invalidated\",\"runId\":\"%s\",\"engineId\":\"%s\","
            "\"cm4ResourceId\":\"%s\",\"cm4ContainerId\":\"%s\",\"cm4Generation\":%llu,"
            "\"helperLaunchId\":\"%s\",\"simulatorResourceId\":\"%s\","
            "\"simulatorContainerId\":\"%s\",\"simulatorGeneration\":%llu,"
            "\"configEpoch\":%llu,\"targetAddress\":\"%s\"}",
            request.cm4.run_id, request.cm4.engine_id, request.cm4.resource_id,
            request.cm4.container_id, (unsigned long long)request.cm4.generation,
            request.helper_launch_id, request.simulator.resource_id,
            request.simulator.container_id, (unsigned long long)request.simulator.generation,
            (unsigned long long)request.config_epoch, request.target_address);
        if (count < 0 || (size_t)count >= response_capacity) return 0;
        *response_length = (size_t)count;
        return 1;
    }
    if (cm4_transport_apply_request(&exchange->target_state, &request) != CM4_TRANSPORT_APPLY_APPLIED) {
        return 0;
    }
    if (exchange->malformed_ack) {
        static const char malformed[] = "{}";
        if (sizeof(malformed) - 1 > response_capacity) return 0;
        memcpy(response, malformed, sizeof(malformed) - 1);
        *response_length = sizeof(malformed) - 1;
        return 1;
    }
    return cm4_transport_write_ack(&exchange->target_state, &request, response,
                                    response_capacity, response_length);
}

static int invoke_frontend(
    const char *input_text,
    size_t input_length,
    memory_exchange_state *exchange,
    int *frontend_result,
    char *output_text,
    size_t output_capacity,
    size_t *output_length)
{
    FILE *input = tmpfile();
    FILE *output = tmpfile();
    FILE *errors = tmpfile();
    size_t count;
    if (input == NULL || output == NULL || errors == NULL) {
        if (input != NULL) fclose(input);
        if (output != NULL) fclose(output);
        if (errors != NULL) fclose(errors);
        return 0;
    }
    if (fwrite(input_text, 1, input_length, input) != input_length || fflush(input) != 0) {
        fclose(input);
        fclose(output);
        fclose(errors);
        return 0;
    }
    rewind(input);
    *frontend_result = cm4_transport_run_control_stdio(input, output, errors, memory_exchange, exchange);
    if (fflush(output) != 0) {
        fclose(input);
        fclose(output);
        fclose(errors);
        return 0;
    }
    rewind(output);
    count = fread(output_text, 1, output_capacity, output);
    if (ferror(output) || count == output_capacity) {
        fclose(input);
        fclose(output);
        fclose(errors);
        return 0;
    }
    output_text[count] = '\0';
    *output_length = count;
    fclose(input);
    fclose(output);
    fclose(errors);
    return 1;
}

int main(int argc, char **argv)
{
    char request_json[4097];
    char expected_ack[4097];
    char invalidation_json[4097];
    char expected_invalidation_ack[4097];
    char actual_ack[4097];
    size_t request_length = 0;
    size_t expected_ack_length = 0;
    size_t invalidation_length = 0;
    size_t expected_invalidation_ack_length = 0;
    size_t actual_ack_length = 0;
    cm4_transport_request request;
    cm4_transport_request invalidation;
    cm4_transport_state state;
    cm4_transport_destination destination;
    const uint8_t loopback[4] = { 127, 0, 0, 1 };
    const uint8_t simulator_source[4] = { 172, 30, 0, 3 };
    const uint8_t foreign_source[4] = { 172, 30, 0, 99 };
    cm4_transport_request updated;
    cm4_transport_state drain_state;
    cm4_transport_request next_generation;
    drain_test_state drain;
    memory_exchange_state exchange;
    char frontend_input[8192];
    char frontend_output[4097];
    size_t frontend_input_length;
    size_t frontend_output_length = 0;
    int frontend_result = -1;
    char malformed_json[4097];
    size_t malformed_length;
    size_t fixture_length;

    if (test_control_path_policy() != 0) return 1;
    CHECK(argc == 5, "apply and invalidate request/ACK fixture paths required");
    CHECK(read_fixture(argv[1], request_json, sizeof(request_json), &request_length), "read request fixture");
    CHECK(read_fixture(argv[2], expected_ack, sizeof(expected_ack), &expected_ack_length), "read ACK fixture");
    CHECK(read_fixture(argv[3], invalidation_json, sizeof(invalidation_json), &invalidation_length),
          "read invalidate request fixture");
    CHECK(read_fixture(argv[4], expected_invalidation_ack, sizeof(expected_invalidation_ack),
                       &expected_invalidation_ack_length), "read invalidate ACK fixture");
    CHECK(cm4_transport_parse_request(request_json, request_length, &request), "parse shared JSONL request");
    CHECK(cm4_transport_parse_request(invalidation_json, invalidation_length, &invalidation),
          "parse shared JSONL invalidation request");
    CHECK(cm4_transport_validate_invalidation_request(&invalidation),
          "validate the existing 13-field invalidation command");
    fixture_length = request_length;
    CHECK(fixture_length > 1 && request_json[fixture_length - 1] == '}', "fixture has one JSON object");
    memcpy(malformed_json, request_json, fixture_length - 1);
    malformed_length = fixture_length - 1;
    memcpy(malformed_json + malformed_length, ",\"extra\":0}", 11);
    malformed_length += 11;
    CHECK(parse_rejected(malformed_json, malformed_length), "reject unknown JSON field");
    memcpy(malformed_json, request_json, fixture_length - 1);
    malformed_length = fixture_length - 1;
    memcpy(malformed_json + malformed_length, ",\"version\":1}", 13);
    malformed_length += 13;
    CHECK(parse_rejected(malformed_json, malformed_length), "reject duplicate JSON field");
    memcpy(malformed_json, request_json, fixture_length);
    malformed_json[fixture_length] = 'x';
    CHECK(parse_rejected(malformed_json, fixture_length + 1), "reject trailing JSON data");
    memset(malformed_json, ' ', sizeof(malformed_json));
    CHECK(parse_rejected(malformed_json, sizeof(malformed_json)), "reject oversized JSON line");

    CHECK(cm4_transport_state_init(&state, &request.cm4, request.helper_launch_id), "initialize owned server state");
    CHECK(cm4_transport_apply_request(&state, &request) == CM4_TRANSPORT_APPLY_APPLIED, "apply current owned target");
    CHECK(state.target_valid && state.config_epoch == 1, "publish current target only after apply");
    CHECK(state.simulator_ipv4[0] == 172 && state.simulator_ipv4[1] == 30 &&
          state.simulator_ipv4[2] == 0 && state.simulator_ipv4[3] == 3, "store applied IPv4 target");

    CHECK(cm4_transport_write_ack(&state, &request, actual_ack, sizeof(actual_ack), &actual_ack_length),
          "serialize ACK only for current applied state");
    CHECK(actual_ack_length == expected_ack_length && memcmp(actual_ack, expected_ack, expected_ack_length) == 0,
          "ACK matches the host protocol fixture");

    CHECK(cm4_transport_route_simulator_feedback(&state, simulator_source, 10, &destination), "route robot 10 feedback");
    CHECK(memcmp(destination.ipv4, loopback, sizeof(loopback)) == 0 && destination.port == 50110,
          "feedback targets matching loopback port");
    CHECK(!cm4_transport_route_simulator_feedback(&state, simulator_source, 11, &destination),
          "reject feedback outside robot range");
    CHECK(!cm4_transport_route_simulator_feedback(&state, foreign_source, 10, &destination),
          "reject feedback from a retired Simulator address");

    {
        poll_dispatch_test_state dispatch = { 1, 0, 0 };
        cm4_transport_dispatch_poll_events(
            UINT32_C(0x07), UINT32_C(0x01), UINT32_C(0x06), 1, 2,
            test_control_ready, test_datagram_ready, &dispatch);
        CHECK(dispatch.queued_datagrams == 0, "control apply drains queued datagrams");
        CHECK(dispatch.control_events == 1, "dispatch the control event once");
        CHECK(dispatch.datagrams_processed == 0,
              "do not dispatch stale UDP readiness after the control handler drains it");
        cm4_transport_dispatch_poll_events(
            UINT32_C(0x01), UINT32_C(0x01), UINT32_C(0x06), 1, 2,
            test_control_ready, test_datagram_ready, &dispatch);
        CHECK(dispatch.control_events == 2,
              "the next control event is handled by a fresh poll dispatch");
    }

    CHECK(cm4_transport_route_cm4_outbound(&state, loopback, 12346, &destination), "route CM4 loopback output");
    CHECK(destination.ipv4[0] == 172 && destination.ipv4[1] == 30 &&
          destination.ipv4[2] == 0 && destination.ipv4[3] == 3 && destination.port == 12346,
          "CM4 output uses current Simulator target");
    CHECK(cm4_transport_route_cm4_outbound(&state, loopback, 49152, &destination),
          "route CM4 loopback output from an ephemeral source port");
    CHECK(destination.ipv4[0] == 172 && destination.ipv4[1] == 30 &&
          destination.ipv4[2] == 0 && destination.ipv4[3] == 3 && destination.port == 12346,
          "ephemeral CM4 source port still targets the fixed Simulator listener");
    CHECK(!cm4_transport_route_cm4_outbound(&state, foreign_source, 49152, &destination),
          "reject CM4 output from a non-loopback source");

    CHECK(cm4_transport_state_init(&drain_state, &request.cm4, request.helper_launch_id),
          "initialize generation-drain state");
    CHECK(cm4_transport_apply_request(&drain_state, &request) == CM4_TRANSPORT_APPLY_APPLIED,
          "initialize old target before testing queue drain");
    memset(&drain, 0, sizeof(drain));
    drain.published_state = &drain_state;
    drain.result = 1;
    next_generation = request;
    next_generation.config_epoch = 2;
    next_generation.simulator.generation = 5;
    copy_literal(next_generation.simulator.container_id, sizeof(next_generation.simulator.container_id),
                 "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
    copy_literal(next_generation.target_address, sizeof(next_generation.target_address), "172.30.0.4");
    CHECK(cm4_transport_apply_request_after_drain(&drain_state, &next_generation,
                                                  test_drain_before_publish, &drain) ==
              CM4_TRANSPORT_APPLY_APPLIED,
          "apply a new generation only after old queued datagrams drain");
    CHECK(drain.calls == 1 && drain.observed_invalidated_state,
          "old target is invalidated and the new epoch reserved before draining");
    CHECK(drain_state.target_valid && drain_state.config_epoch == 2 &&
          drain_state.simulator.generation == 5 && drain_state.simulator_ipv4[3] == 4,
          "publish the new generation after drain succeeds");
    CHECK(cm4_transport_write_ack(&drain_state, &next_generation, actual_ack,
                                  sizeof(actual_ack), &actual_ack_length),
          "acknowledge the new generation only after drain succeeds");

    drain.result = 0;
    next_generation.config_epoch = 3;
    next_generation.simulator.generation = 6;
    copy_literal(next_generation.simulator.container_id, sizeof(next_generation.simulator.container_id),
                 "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd");
    copy_literal(next_generation.target_address, sizeof(next_generation.target_address), "172.30.0.5");
    CHECK(cm4_transport_apply_request_after_drain(&drain_state, &next_generation,
                                                  test_drain_before_publish, &drain) ==
              CM4_TRANSPORT_APPLY_DRAIN_FAILED,
          "fail closed when the old UDP queue cannot be drained");
    CHECK(!drain_state.target_valid && drain_state.config_epoch == 3,
          "failed drain invalidates old and candidate routes without reverting epoch");
    CHECK(!cm4_transport_write_ack(&drain_state, &next_generation, actual_ack,
                                   sizeof(actual_ack), &actual_ack_length),
          "do not acknowledge a generation whose queue did not drain");
    CHECK(!cm4_transport_route_cm4_outbound(&drain_state, loopback, 12346, &destination),
          "no previous destination is used after a failed drain");

    updated = request;
    updated.config_epoch = 2;
    copy_literal(updated.target_address, sizeof(updated.target_address), "172.30.0.4");
    CHECK(cm4_transport_apply_request(&state, &updated) == CM4_TRANSPORT_APPLY_APPLIED, "apply newer epoch");
    CHECK(!cm4_transport_write_ack(&state, &request, actual_ack, sizeof(actual_ack), &actual_ack_length),
          "do not acknowledge an older configuration epoch");
    CHECK(cm4_transport_apply_request(&state, &request) == CM4_TRANSPORT_APPLY_REJECTED,
          "reject late request from an older epoch");
    CHECK(state.target_valid && state.config_epoch == 2 && state.simulator_ipv4[3] == 4,
          "late request cannot roll back the active target");

    updated.config_epoch = 3;
    copy_literal(updated.cm4.engine_id, sizeof(updated.cm4.engine_id), "foreign-engine");
    CHECK(cm4_transport_apply_request(&state, &updated) == CM4_TRANSPORT_APPLY_REJECTED,
          "reject a request for a foreign engine");
    CHECK(state.target_valid && state.config_epoch == 2 && state.simulator_ipv4[3] == 4,
          "foreign request cannot disturb the current target");
    updated = request;
    updated.config_epoch = 3;
    copy_literal(updated.helper_launch_id, sizeof(updated.helper_launch_id), "foreign-launch");
    CHECK(cm4_transport_apply_request(&state, &updated) == CM4_TRANSPORT_APPLY_REJECTED,
          "reject a request from a different helper launch");
    CHECK(state.target_valid && state.config_epoch == 2, "foreign helper cannot invalidate the current target");

    updated = request;
    updated.config_epoch = 3;
    copy_literal(updated.target_address, sizeof(updated.target_address), "255.255.255.255");
    CHECK(cm4_transport_apply_request(&state, &updated) == CM4_TRANSPORT_APPLY_REJECTED,
          "reject invalid target after a new epoch begins");
    CHECK(!state.target_valid && state.config_epoch == 3, "invalid newer target does not restore an older target");
    CHECK(!cm4_transport_route_cm4_outbound(&state, loopback, 12346, &destination),
          "invalidated target is not used for outbound traffic");

    CHECK(cm4_transport_invalidate(&state, 4), "invalidate by newer epoch");
    CHECK(!state.target_valid && state.config_epoch == 4, "invalidation clears route state");
    CHECK(!cm4_transport_invalidate(&state, 3), "reject stale invalidation");

    CHECK(cm4_transport_state_init(&drain_state, &request.cm4, request.helper_launch_id),
          "initialize the native state for remote invalidation");
    CHECK(cm4_transport_apply_request(&drain_state, &request) == CM4_TRANSPORT_APPLY_APPLIED,
          "apply the route that remote invalidation will retire");
    drain.published_state = &drain_state;
    drain.calls = 0;
    drain.result = 1;
    drain.observed_invalidated_state = 0;
    CHECK(cm4_transport_invalidate_request_after_drain(
              &drain_state, &invalidation, test_drain_before_publish, &drain) == CM4_TRANSPORT_APPLY_APPLIED,
          "invalidate the owned route through the remote command contract");
    CHECK(drain.calls == 1 && drain.observed_invalidated_state,
          "invalidate the old route and advance its epoch before draining");
    CHECK(!drain_state.target_valid && drain_state.config_epoch == invalidation.config_epoch,
          "remote invalidation leaves no route at its acknowledged epoch");
    CHECK(cm4_transport_write_invalidation_ack(&drain_state, &invalidation,
                                                actual_ack, sizeof(actual_ack), &actual_ack_length),
          "write the invalidated ACK only after the route is drained");
    CHECK(actual_ack_length == expected_invalidation_ack_length &&
          memcmp(actual_ack, expected_invalidation_ack, actual_ack_length) == 0,
          "native invalidation ACK matches the shared wire fixture");
    CHECK(cm4_transport_apply_request(&drain_state, &request) == CM4_TRANSPORT_APPLY_REJECTED &&
          !drain_state.target_valid && drain_state.config_epoch == invalidation.config_epoch,
          "an old apply request cannot restore the route after remote invalidation");
    drain.calls = 0;
    CHECK(cm4_transport_invalidate_request_after_drain(
              &drain_state, &invalidation, test_drain_before_publish, &drain) == CM4_TRANSPORT_APPLY_REJECTED &&
          drain.calls == 0 && !drain_state.target_valid,
          "an equal-epoch repeated invalidation is rejected without another drain");

    CHECK(cm4_transport_state_init(&drain_state, &request.cm4, request.helper_launch_id),
          "reset the native state for failed invalidation drain");
    CHECK(cm4_transport_apply_request(&drain_state, &request) == CM4_TRANSPORT_APPLY_APPLIED,
          "reapply the route before a failed invalidation drain");
    drain.published_state = &drain_state;
    drain.calls = 0;
    drain.result = 0;
    drain.observed_invalidated_state = 0;
    CHECK(cm4_transport_invalidate_request_after_drain(
              &drain_state, &invalidation, test_drain_before_publish, &drain) == CM4_TRANSPORT_APPLY_DRAIN_FAILED,
          "report a failed invalidation drain without restoring the route");
    CHECK(drain.calls == 1 && drain.observed_invalidated_state &&
          !drain_state.target_valid && drain_state.config_epoch == invalidation.config_epoch,
          "a failed drain remains invalidated at the newer epoch");
    CHECK(!cm4_transport_write_invalidation_ack(&drain_state, &invalidation,
                                                 actual_ack, sizeof(actual_ack), &actual_ack_length),
          "do not acknowledge invalidation when the drain did not complete");

    CHECK(cm4_transport_state_init(&drain_state, &request.cm4, request.helper_launch_id),
          "reset the native state for rejected invalidation requests");
    CHECK(cm4_transport_apply_request(&drain_state, &request) == CM4_TRANSPORT_APPLY_APPLIED,
          "reapply the owned route before checking stale invalidation");
    updated = invalidation;
    updated.config_epoch = request.config_epoch;
    drain.published_state = &drain_state;
    drain.calls = 0;
    drain.result = 1;
    CHECK(cm4_transport_invalidate_request_after_drain(
              &drain_state, &updated, test_drain_before_publish, &drain) == CM4_TRANSPORT_APPLY_REJECTED,
          "reject a stale remote invalidation request");
    CHECK(drain.calls == 0 && drain_state.target_valid && drain_state.config_epoch == request.config_epoch,
          "a stale invalidation cannot change the current route");
    updated = invalidation;
    copy_literal(updated.helper_launch_id, sizeof(updated.helper_launch_id), "foreign-launch");
    CHECK(cm4_transport_invalidate_request_after_drain(
              &drain_state, &updated, test_drain_before_publish, &drain) == CM4_TRANSPORT_APPLY_REJECTED,
          "reject invalidation from a different helper launch");
    CHECK(drain.calls == 0 && drain_state.target_valid && drain_state.config_epoch == request.config_epoch,
          "a foreign invalidation cannot change the current route");
    updated = invalidation;
    copy_literal(updated.cm4.engine_id, sizeof(updated.cm4.engine_id), "foreign-engine");
    CHECK(cm4_transport_invalidate_request_after_drain(
              &drain_state, &updated, test_drain_before_publish, &drain) == CM4_TRANSPORT_APPLY_REJECTED,
          "reject invalidation for a different owned CM4 identity");
    CHECK(drain.calls == 0 && drain_state.target_valid && drain_state.config_epoch == request.config_epoch,
          "a foreign CM4 identity cannot change the current route");
    updated = invalidation;
    copy_literal(updated.type, sizeof(updated.type), "unknownCommand");
    CHECK(!cm4_transport_validate_invalidation_request(&updated),
          "reject an unknown control command instead of interpreting it as invalidation");

    memset(&exchange, 0, sizeof(exchange));
    exchange.available = 1;
    CHECK(cm4_transport_state_init(&exchange.target_state, &request.cm4, request.helper_launch_id),
          "initialize the exchange seam with current owner identity");
    memcpy(frontend_input, request_json, request_length);
    frontend_input[request_length] = '\n';
    frontend_input_length = request_length + 1;
    CHECK(invoke_frontend(frontend_input, frontend_input_length, &exchange,
                          &frontend_result, frontend_output, sizeof(frontend_output) - 1,
                          &frontend_output_length), "run actual stdio frontend with memory exchange");
    CHECK(frontend_result == 0 && exchange.calls == 1, "frontend accepted an applied ACK");
    CHECK(frontend_output_length == expected_ack_length + 1 &&
          memcmp(frontend_output, expected_ack, expected_ack_length) == 0 &&
          frontend_output[frontend_output_length - 1] == '\n',
          "frontend emits exactly the shared host ACK line");

    exchange.available = 0;
    exchange.calls = 0;
    frontend_output_length = 0;
    CHECK(invoke_frontend(frontend_input, frontend_input_length, &exchange,
                          &frontend_result, frontend_output, sizeof(frontend_output) - 1,
                          &frontend_output_length), "run frontend with absent server seam");
    CHECK(frontend_result != 0 && exchange.calls == 1 && frontend_output_length == 0,
          "an absent server cannot produce an applied ACK");

    exchange.available = 1;
    exchange.calls = 0;
    exchange.malformed_ack = 1;
    CHECK(cm4_transport_state_init(&exchange.target_state, &request.cm4, request.helper_launch_id),
          "reset the exchange server before malformed ACK case");
    frontend_output_length = 0;
    CHECK(invoke_frontend(frontend_input, frontend_input_length, &exchange,
                          &frontend_result, frontend_output, sizeof(frontend_output) - 1,
                          &frontend_output_length), "run frontend with malformed ACK seam");
    CHECK(frontend_result != 0 && exchange.calls == 1 && frontend_output_length == 0,
          "a malformed ACK is not written to stdout");

    exchange.malformed_ack = 0;
    exchange.calls = 0;
    frontend_output_length = 0;
    frontend_input[request_length + 1] = '{';
    frontend_input[request_length + 2] = '}';
    frontend_input[request_length + 3] = '\n';
    CHECK(invoke_frontend(frontend_input, request_length + 4, &exchange,
                          &frontend_result, frontend_output, sizeof(frontend_output) - 1,
                          &frontend_output_length), "run frontend with an extra input line");
    CHECK(frontend_result != 0 && exchange.calls == 0 && frontend_output_length == 0,
          "extra input lines are rejected before exchange");

    memset(frontend_input, 'x', 4097);
    frontend_input[4097] = '\n';
    exchange.calls = 0;
    frontend_output_length = 0;
    CHECK(invoke_frontend(frontend_input, 4098, &exchange,
                          &frontend_result, frontend_output, sizeof(frontend_output) - 1,
                          &frontend_output_length), "run frontend with an oversized line");
    CHECK(frontend_result != 0 && exchange.calls == 0 && frontend_output_length == 0,
          "oversized input is rejected before exchange");

    memset(&exchange, 0, sizeof(exchange));
    exchange.available = 1;
    CHECK(cm4_transport_state_init(&exchange.target_state, &request.cm4, request.helper_launch_id),
          "initialize the exchange state before remote invalidation");
    CHECK(cm4_transport_apply_request(&exchange.target_state, &request) == CM4_TRANSPORT_APPLY_APPLIED,
          "establish the prior owned route before remote invalidation");
    memcpy(frontend_input, invalidation_json, invalidation_length);
    frontend_input[invalidation_length] = '\n';
    frontend_input_length = invalidation_length + 1;
    frontend_output_length = 0;
    CHECK(invoke_frontend(frontend_input, frontend_input_length, &exchange,
                          &frontend_result, frontend_output, sizeof(frontend_output) - 1,
                          &frontend_output_length), "run actual stdio frontend for remote invalidation");
    CHECK(frontend_result == 0 && exchange.calls == 1,
          "frontend accepts the existing 13-field remote invalidate command");
    CHECK(frontend_output_length == expected_invalidation_ack_length + 1 &&
          memcmp(frontend_output, expected_invalidation_ack, expected_invalidation_ack_length) == 0 &&
          frontend_output[frontend_output_length - 1] == '\n',
          "frontend emits exactly the matching invalidated acknowledgement");

    return 0;
}
