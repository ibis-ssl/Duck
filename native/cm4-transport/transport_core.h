#ifndef DUCK_CM4_TRANSPORT_CORE_H
#define DUCK_CM4_TRANSPORT_CORE_H

#include <stddef.h>
#include <stdint.h>

#define CM4_TRANSPORT_TEXT_CAPACITY 129
#define CM4_TRANSPORT_CONTAINER_ID_LENGTH 64
#define CM4_TRANSPORT_IPV4_TEXT_CAPACITY 16

typedef struct cm4_transport_identity {
    char run_id[CM4_TRANSPORT_TEXT_CAPACITY];
    char engine_id[CM4_TRANSPORT_TEXT_CAPACITY];
    char resource_id[CM4_TRANSPORT_TEXT_CAPACITY];
    char container_id[CM4_TRANSPORT_CONTAINER_ID_LENGTH + 1];
    uint64_t generation;
} cm4_transport_identity;

typedef struct cm4_transport_request {
    uint32_t version;
    char type[32];
    cm4_transport_identity cm4;
    char helper_launch_id[CM4_TRANSPORT_TEXT_CAPACITY];
    cm4_transport_identity simulator;
    uint64_t config_epoch;
    char target_address[CM4_TRANSPORT_IPV4_TEXT_CAPACITY];
} cm4_transport_request;

typedef struct cm4_transport_state {
    cm4_transport_identity cm4;
    char helper_launch_id[CM4_TRANSPORT_TEXT_CAPACITY];
    uint64_t config_epoch;
    int target_valid;
    cm4_transport_identity simulator;
    uint8_t simulator_ipv4[4];
    uint64_t last_simulator_generation;
    char last_simulator_container_id[CM4_TRANSPORT_CONTAINER_ID_LENGTH + 1];
} cm4_transport_state;

typedef struct cm4_transport_destination {
    uint8_t ipv4[4];
    uint16_t port;
} cm4_transport_destination;

typedef enum cm4_transport_apply_status {
    CM4_TRANSPORT_APPLY_REJECTED = 0,
    CM4_TRANSPORT_APPLY_APPLIED = 1,
    CM4_TRANSPORT_APPLY_DRAIN_FAILED = 2
} cm4_transport_apply_status;

typedef int (*cm4_transport_drain_fn)(void *context);
typedef void (*cm4_transport_control_ready_fn)(void *context);
typedef void (*cm4_transport_datagram_ready_fn)(void *context, unsigned int index);

int cm4_transport_state_init(
    cm4_transport_state *state,
    const cm4_transport_identity *cm4,
    const char *helper_launch_id);

int cm4_transport_parse_ipv4_address(const char *text, uint8_t address[4]);

cm4_transport_apply_status cm4_transport_apply_request(
    cm4_transport_state *state,
    const cm4_transport_request *request);
cm4_transport_apply_status cm4_transport_apply_request_after_drain(
    cm4_transport_state *state,
    const cm4_transport_request *request,
    cm4_transport_drain_fn drain,
    void *context);

int cm4_transport_parse_request(
    const char *json_line,
    size_t length,
    cm4_transport_request *request);

int cm4_transport_write_ack(
    const cm4_transport_state *state,
    const cm4_transport_request *request,
    char *output,
    size_t capacity,
    size_t *written);

int cm4_transport_invalidate(cm4_transport_state *state, uint64_t next_epoch);

void cm4_transport_dispatch_poll_events(
    uint32_t ready_mask,
    uint32_t control_mask,
    uint32_t datagram_mask,
    unsigned int first_datagram_bit,
    unsigned int datagram_count,
    cm4_transport_control_ready_fn control_ready,
    cm4_transport_datagram_ready_fn datagram_ready,
    void *context);

int cm4_transport_route_simulator_feedback(
    const cm4_transport_state *state,
    const uint8_t source_ipv4[4],
    uint8_t robot_id,
    cm4_transport_destination *destination);

int cm4_transport_route_cm4_outbound(
    const cm4_transport_state *state,
    const uint8_t source_ipv4[4],
    uint16_t source_port,
    cm4_transport_destination *destination);

#endif
