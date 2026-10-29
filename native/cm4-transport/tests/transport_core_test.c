#include "../transport_core.h"
#include "../transport_control_frontend.h"

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
        !cm4_transport_parse_request(request_json, request_length, &request) ||
        cm4_transport_apply_request(&exchange->target_state, &request) != CM4_TRANSPORT_APPLY_APPLIED) {
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
    char actual_ack[4097];
    size_t request_length = 0;
    size_t expected_ack_length = 0;
    size_t actual_ack_length = 0;
    cm4_transport_request request;
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

    CHECK(argc == 3, "request and acknowledgement fixture paths required");
    CHECK(read_fixture(argv[1], request_json, sizeof(request_json), &request_length), "read request fixture");
    CHECK(read_fixture(argv[2], expected_ack, sizeof(expected_ack), &expected_ack_length), "read ACK fixture");
    CHECK(cm4_transport_parse_request(request_json, request_length, &request), "parse shared JSONL request");
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

    CHECK(cm4_transport_route_cm4_outbound(&state, loopback, 12346, &destination), "route CM4 loopback output");
    CHECK(destination.ipv4[0] == 172 && destination.ipv4[1] == 30 &&
          destination.ipv4[2] == 0 && destination.ipv4[3] == 3 && destination.port == 12346,
          "CM4 output uses current Simulator target");

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

    return 0;
}
