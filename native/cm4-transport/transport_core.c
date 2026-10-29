#include "transport_core.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define CM4_TRANSPORT_MAX_JSON_BYTES 4096

typedef struct json_cursor {
    const char *text;
    size_t length;
    size_t offset;
} json_cursor;

static int is_hex_character(char value)
{
    return (value >= '0' && value <= '9') ||
        (value >= 'a' && value <= 'f') ||
        (value >= 'A' && value <= 'F');
}

static int is_full_container_id(const char *value)
{
    size_t index;
    if (value == NULL || strlen(value) != CM4_TRANSPORT_CONTAINER_ID_LENGTH) {
        return 0;
    }

    for (index = 0; index < CM4_TRANSPORT_CONTAINER_ID_LENGTH; ++index) {
        if (!is_hex_character(value[index])) return 0;
    }
    return 1;
}

static int same_identity(
    const cm4_transport_identity *left,
    const cm4_transport_identity *right)
{
    return left != NULL && right != NULL &&
        left->generation == right->generation &&
        strcmp(left->run_id, right->run_id) == 0 &&
        strcmp(left->engine_id, right->engine_id) == 0 &&
        strcmp(left->resource_id, right->resource_id) == 0 &&
        strcmp(left->container_id, right->container_id) == 0;
}

static void skip_whitespace(json_cursor *cursor)
{
    while (cursor->offset < cursor->length) {
        char value = cursor->text[cursor->offset];
        if (value != ' ' && value != '\t' && value != '\r' && value != '\n') break;
        ++cursor->offset;
    }
}

static int consume(json_cursor *cursor, char expected)
{
    skip_whitespace(cursor);
    if (cursor->offset >= cursor->length || cursor->text[cursor->offset] != expected) return 0;
    ++cursor->offset;
    return 1;
}

static int parse_string(json_cursor *cursor, char *output, size_t capacity)
{
    size_t start;
    size_t length;
    size_t index;

    if (!consume(cursor, '"')) return 0;
    start = cursor->offset;
    while (cursor->offset < cursor->length && cursor->text[cursor->offset] != '"') {
        unsigned char value = (unsigned char)cursor->text[cursor->offset];
        if (value < 0x20 || value == '\\') return 0;
        ++cursor->offset;
    }
    if (cursor->offset >= cursor->length) return 0;
    length = cursor->offset - start;
    if (length == 0 || length >= capacity) return 0;
    for (index = 0; index < length; ++index) {
        if ((unsigned char)cursor->text[start + index] > 0x7e) return 0;
    }

    memcpy(output, cursor->text + start, length);
    output[length] = '\0';
    ++cursor->offset;
    return 1;
}

static int parse_uint64(json_cursor *cursor, uint64_t *output)
{
    uint64_t value = 0;
    size_t digits = 0;

    skip_whitespace(cursor);
    while (cursor->offset < cursor->length) {
        char character = cursor->text[cursor->offset];
        uint64_t digit;
        if (character < '0' || character > '9') break;
        digit = (uint64_t)(character - '0');
        if (value > (UINT64_MAX - digit) / 10) return 0;
        value = value * 10 + digit;
        ++cursor->offset;
        ++digits;
    }

    if (digits == 0 || (digits > 1 && cursor->text[cursor->offset - digits] == '0')) return 0;
    *output = value;
    return 1;
}

int cm4_transport_parse_ipv4_address(const char *text, uint8_t address[4])
{
    unsigned int part = 0;
    unsigned int value = 0;
    unsigned int digits = 0;

    if (text == NULL || address == NULL) return 0;
    while (*text != '\0') {
        if (*text >= '0' && *text <= '9') {
            if (++digits > 3) return 0;
            value = value * 10 + (unsigned int)(*text - '0');
            if (value > 255) return 0;
        } else if (*text == '.' && digits > 0 && part < 3) {
            address[part++] = (uint8_t)value;
            value = 0;
            digits = 0;
        } else {
            return 0;
        }
        ++text;
    }
    if (part != 3 || digits == 0) return 0;
    address[3] = (uint8_t)value;

    if (address[0] == 0 || address[0] == 127 || address[0] >= 224 ||
        (address[0] == 169 && address[1] == 254) ||
        (address[0] == 255 && address[1] == 255 && address[2] == 255 && address[3] == 255)) {
        return 0;
    }
    return 1;
}

static int valid_helper_id(const char *value)
{
    size_t index;
    size_t length;
    if (value == NULL) return 0;
    length = strlen(value);
    if (length == 0 || length >= CM4_TRANSPORT_TEXT_CAPACITY) return 0;
    for (index = 0; index < length; ++index) {
        char character = value[index];
        if (!((character >= 'a' && character <= 'z') ||
              (character >= 'A' && character <= 'Z') ||
              (character >= '0' && character <= '9') || character == '-' || character == '_')) {
            return 0;
        }
    }
    return 1;
}

static int copy_text(char *destination, size_t capacity, const char *source)
{
    size_t length;

    if (destination == NULL || source == NULL || capacity == 0) {
        return 0;
    }

    length = strlen(source);
    if (length == 0 || length >= capacity) {
        return 0;
    }

    memcpy(destination, source, length + 1);
    return 1;
}

static int copy_identity(
    cm4_transport_identity *destination,
    const cm4_transport_identity *source)
{
    if (destination == NULL || source == NULL || source->generation == 0 ||
        !is_full_container_id(source->container_id)) {
        return 0;
    }

    memset(destination, 0, sizeof(*destination));
    if (!copy_text(destination->run_id, sizeof(destination->run_id), source->run_id) ||
        !copy_text(destination->engine_id, sizeof(destination->engine_id), source->engine_id) ||
        !copy_text(destination->resource_id, sizeof(destination->resource_id), source->resource_id) ||
        !copy_text(destination->container_id, sizeof(destination->container_id), source->container_id)) {
        return 0;
    }

    destination->generation = source->generation;
    return 1;
}

int cm4_transport_state_init(
    cm4_transport_state *state,
    const cm4_transport_identity *cm4,
    const char *helper_launch_id)
{
    if (state == NULL || cm4 == NULL || helper_launch_id == NULL) {
        return 0;
    }

    memset(state, 0, sizeof(*state));
    if (!copy_identity(&state->cm4, cm4) ||
        !copy_text(state->helper_launch_id, sizeof(state->helper_launch_id), helper_launch_id)) {
        memset(state, 0, sizeof(*state));
        return 0;
    }

    return 1;
}

cm4_transport_apply_status cm4_transport_apply_request(
    cm4_transport_state *state,
    const cm4_transport_request *request)
{
    uint8_t address[4];

    if (state == NULL || request == NULL || request->version != 1 ||
        strcmp(request->type, "applyTarget") != 0 ||
        !same_identity(&state->cm4, &request->cm4) ||
        strcmp(state->helper_launch_id, request->helper_launch_id) != 0 ||
        !valid_helper_id(request->helper_launch_id) || request->config_epoch == 0 ||
        request->config_epoch <= state->config_epoch) {
        return CM4_TRANSPORT_APPLY_REJECTED;
    }

    state->config_epoch = request->config_epoch;
    state->target_valid = 0;
    memset(&state->simulator, 0, sizeof(state->simulator));
    memset(state->simulator_ipv4, 0, sizeof(state->simulator_ipv4));

    if (strcmp(request->simulator.run_id, state->cm4.run_id) != 0 ||
        strcmp(request->simulator.engine_id, state->cm4.engine_id) != 0 ||
        request->simulator.generation == 0 ||
        strcmp(request->simulator.container_id, state->cm4.container_id) == 0 ||
        strcmp(request->simulator.resource_id, state->cm4.resource_id) == 0 ||
        !is_full_container_id(request->simulator.container_id) ||
        (state->last_simulator_generation > 0 &&
         ((strcmp(state->last_simulator_container_id, request->simulator.container_id) == 0 &&
           request->simulator.generation != state->last_simulator_generation) ||
          (strcmp(state->last_simulator_container_id, request->simulator.container_id) != 0 &&
           request->simulator.generation <= state->last_simulator_generation))) ||
        !cm4_transport_parse_ipv4_address(request->target_address, address)) {
        return CM4_TRANSPORT_APPLY_REJECTED;
    }

    if (!copy_identity(&state->simulator, &request->simulator)) {
        return CM4_TRANSPORT_APPLY_REJECTED;
    }
    memcpy(state->simulator_ipv4, address, sizeof(address));
    state->last_simulator_generation = request->simulator.generation;
    memcpy(state->last_simulator_container_id, request->simulator.container_id,
           sizeof(state->last_simulator_container_id));
    state->target_valid = 1;
    return CM4_TRANSPORT_APPLY_APPLIED;
}

cm4_transport_apply_status cm4_transport_apply_request_after_drain(
    cm4_transport_state *state,
    const cm4_transport_request *request,
    cm4_transport_drain_fn drain,
    void *context)
{
    cm4_transport_state candidate;
    cm4_transport_apply_status status;
    uint64_t previous_epoch;

    if (state == NULL || request == NULL) return CM4_TRANSPORT_APPLY_REJECTED;
    previous_epoch = state->config_epoch;
    candidate = *state;
    status = cm4_transport_apply_request(&candidate, request);

    if (candidate.config_epoch > previous_epoch) {
        state->config_epoch = candidate.config_epoch;
        state->target_valid = 0;
        memset(&state->simulator, 0, sizeof(state->simulator));
        memset(state->simulator_ipv4, 0, sizeof(state->simulator_ipv4));
        if (drain == NULL || !drain(context)) {
            candidate.target_valid = 0;
            memset(&candidate.simulator, 0, sizeof(candidate.simulator));
            memset(candidate.simulator_ipv4, 0, sizeof(candidate.simulator_ipv4));
            *state = candidate;
            return CM4_TRANSPORT_APPLY_DRAIN_FAILED;
        }
        *state = candidate;
    }

    return status;
}

int cm4_transport_parse_request(
    const char *json_line,
    size_t length,
    cm4_transport_request *request)
{
    static const char *const property_names[13] = {
        "version", "type", "runId", "engineId", "cm4ResourceId", "cm4ContainerId",
        "cm4Generation", "helperLaunchId", "simulatorResourceId", "simulatorContainerId",
        "simulatorGeneration", "configEpoch", "targetAddress"
    };
    cm4_transport_request parsed;
    json_cursor cursor;
    uint16_t seen = 0;
    int property_count = 0;

    if (json_line == NULL || request == NULL || length == 0 || length > CM4_TRANSPORT_MAX_JSON_BYTES) return 0;
    memset(&parsed, 0, sizeof(parsed));
    cursor.text = json_line;
    cursor.length = length;
    cursor.offset = 0;
    if (!consume(&cursor, '{')) return 0;

    while (property_count < 13) {
        char key[40];
        int index = -1;
        size_t candidate;
        uint64_t number;
        char string_value[CM4_TRANSPORT_TEXT_CAPACITY];

        if (!parse_string(&cursor, key, sizeof(key)) || !consume(&cursor, ':')) return 0;
        for (candidate = 0; candidate < 13; ++candidate) {
            if (strcmp(key, property_names[candidate]) == 0) {
                index = (int)candidate;
                break;
            }
        }
        if (index < 0 || (seen & (uint16_t)(1u << index)) != 0) return 0;
        seen |= (uint16_t)(1u << index);

        switch (index) {
        case 0:
            if (!parse_uint64(&cursor, &number) || number > UINT32_MAX) return 0;
            parsed.version = (uint32_t)number;
            break;
        case 1:
            if (!parse_string(&cursor, parsed.type, sizeof(parsed.type))) return 0;
            break;
        case 2:
            if (!parse_string(&cursor, string_value, sizeof(string_value))) return 0;
            if (!copy_text(parsed.cm4.run_id, sizeof(parsed.cm4.run_id), string_value) ||
                !copy_text(parsed.simulator.run_id, sizeof(parsed.simulator.run_id), string_value)) return 0;
            break;
        case 3:
            if (!parse_string(&cursor, string_value, sizeof(string_value))) return 0;
            if (!copy_text(parsed.cm4.engine_id, sizeof(parsed.cm4.engine_id), string_value) ||
                !copy_text(parsed.simulator.engine_id, sizeof(parsed.simulator.engine_id), string_value)) return 0;
            break;
        case 4:
            if (!parse_string(&cursor, parsed.cm4.resource_id, sizeof(parsed.cm4.resource_id))) return 0;
            break;
        case 5:
            if (!parse_string(&cursor, parsed.cm4.container_id, sizeof(parsed.cm4.container_id))) return 0;
            break;
        case 6:
            if (!parse_uint64(&cursor, &parsed.cm4.generation)) return 0;
            break;
        case 7:
            if (!parse_string(&cursor, parsed.helper_launch_id, sizeof(parsed.helper_launch_id))) return 0;
            break;
        case 8:
            if (!parse_string(&cursor, parsed.simulator.resource_id, sizeof(parsed.simulator.resource_id))) return 0;
            break;
        case 9:
            if (!parse_string(&cursor, parsed.simulator.container_id, sizeof(parsed.simulator.container_id))) return 0;
            break;
        case 10:
            if (!parse_uint64(&cursor, &parsed.simulator.generation)) return 0;
            break;
        case 11:
            if (!parse_uint64(&cursor, &parsed.config_epoch)) return 0;
            break;
        case 12:
            if (!parse_string(&cursor, parsed.target_address, sizeof(parsed.target_address))) return 0;
            break;
        default:
            return 0;
        }

        ++property_count;
        skip_whitespace(&cursor);
        if (property_count < 13) {
            if (!consume(&cursor, ',')) return 0;
        }
    }

    if (seen != 0x1fffu || !consume(&cursor, '}')) return 0;
    skip_whitespace(&cursor);
    if (cursor.offset != cursor.length) return 0;
    *request = parsed;
    return 1;
}

int cm4_transport_write_ack(
    const cm4_transport_state *state,
    const cm4_transport_request *request,
    char *output,
    size_t capacity,
    size_t *written)
{
    int count;
    uint8_t address[4];

    if (state == NULL || request == NULL || output == NULL || written == NULL || capacity == 0 ||
        request->version != 1 || !valid_helper_id(request->helper_launch_id) ||
        !is_full_container_id(request->cm4.container_id) ||
        !is_full_container_id(request->simulator.container_id) ||
        !cm4_transport_parse_ipv4_address(request->target_address, address) || !state->target_valid ||
        state->config_epoch != request->config_epoch ||
        !same_identity(&state->cm4, &request->cm4) ||
        !same_identity(&state->simulator, &request->simulator) ||
        strcmp(state->helper_launch_id, request->helper_launch_id) != 0 ||
        memcmp(state->simulator_ipv4, address, sizeof(address)) != 0) {
        return 0;
    }

    count = snprintf(output, capacity,
        "{\"version\":1,\"type\":\"applied\",\"runId\":\"%s\",\"engineId\":\"%s\","
        "\"cm4ResourceId\":\"%s\",\"cm4ContainerId\":\"%s\",\"cm4Generation\":%llu,"
        "\"helperLaunchId\":\"%s\",\"simulatorResourceId\":\"%s\","
        "\"simulatorContainerId\":\"%s\",\"simulatorGeneration\":%llu,"
        "\"configEpoch\":%llu,\"targetAddress\":\"%s\"}",
        request->cm4.run_id, request->cm4.engine_id, request->cm4.resource_id,
        request->cm4.container_id, (unsigned long long)request->cm4.generation,
        request->helper_launch_id, request->simulator.resource_id, request->simulator.container_id,
        (unsigned long long)request->simulator.generation,
        (unsigned long long)request->config_epoch, request->target_address);
    if (count < 0 || (size_t)count >= capacity) return 0;
    *written = (size_t)count;
    return 1;
}

int cm4_transport_invalidate(cm4_transport_state *state, uint64_t next_epoch)
{
    if (state == NULL || next_epoch <= state->config_epoch) return 0;
    state->config_epoch = next_epoch;
    state->target_valid = 0;
    memset(&state->simulator, 0, sizeof(state->simulator));
    memset(state->simulator_ipv4, 0, sizeof(state->simulator_ipv4));
    return 1;
}

int cm4_transport_route_simulator_feedback(
    const cm4_transport_state *state,
    const uint8_t source_ipv4[4],
    uint8_t robot_id,
    cm4_transport_destination *destination)
{
    static const uint8_t loopback[4] = { 127, 0, 0, 1 };
    if (state == NULL || source_ipv4 == NULL || destination == NULL || !state->target_valid ||
        robot_id > 10 || memcmp(source_ipv4, state->simulator_ipv4, 4) != 0) return 0;
    memcpy(destination->ipv4, loopback, sizeof(loopback));
    destination->port = (uint16_t)(50100u + robot_id);
    return 1;
}

int cm4_transport_route_cm4_outbound(
    const cm4_transport_state *state,
    const uint8_t source_ipv4[4],
    uint16_t source_port,
    cm4_transport_destination *destination)
{
    static const uint8_t loopback[4] = { 127, 0, 0, 1 };
    if (state == NULL || source_ipv4 == NULL || destination == NULL || !state->target_valid ||
        source_port != 12346 || memcmp(source_ipv4, loopback, sizeof(loopback)) != 0) return 0;
    memcpy(destination->ipv4, state->simulator_ipv4, sizeof(destination->ipv4));
    destination->port = 12346;
    return 1;
}
