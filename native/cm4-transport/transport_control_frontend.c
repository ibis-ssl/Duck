#include "transport_control_frontend.h"

#include "transport_core.h"

#include <string.h>

#define MAX_CONTROL_LINE 4096

static int read_request_line(FILE *input, char *buffer, size_t capacity, size_t *length)
{
    size_t used = 0;
    int value;
    int saw_newline = 0;

    while ((value = fgetc(input)) != EOF) {
        if (saw_newline || used >= MAX_CONTROL_LINE) return 0;
        if (value == '\n') {
            saw_newline = 1;
        } else {
            buffer[used++] = (char)value;
        }
    }

    if (ferror(input) || !saw_newline || used == 0 || used + 1 > capacity) return 0;
    buffer[used] = '\0';
    *length = used;
    return 1;
}

int cm4_transport_run_control_stdio(
    FILE *input,
    FILE *output,
    FILE *errors,
    cm4_transport_exchange_fn exchange,
    void *exchange_context)
{
    char request_line[MAX_CONTROL_LINE + 1];
    char expected_ack[MAX_CONTROL_LINE + 1];
    char response[MAX_CONTROL_LINE + 1];
    size_t request_length = 0;
    size_t expected_ack_length = 0;
    size_t response_length = 0;
    cm4_transport_request request;
    cm4_transport_state validation_state;

    if (input == NULL || output == NULL || errors == NULL || exchange == NULL ||
        !read_request_line(input, request_line, sizeof(request_line), &request_length) ||
        !cm4_transport_parse_request(request_line, request_length, &request)) {
        fputs("control request was rejected\n", errors);
        return 2;
    }

    if (strcmp(request.type, "applyTarget") == 0) {
        if (!cm4_transport_state_init(&validation_state, &request.cm4, request.helper_launch_id) ||
            cm4_transport_apply_request(&validation_state, &request) != CM4_TRANSPORT_APPLY_APPLIED ||
            !cm4_transport_write_ack(&validation_state, &request, expected_ack,
                                     sizeof(expected_ack), &expected_ack_length)) {
            fputs("control request was rejected\n", errors);
            return 2;
        }
    } else if (strcmp(request.type, "invalidateTarget") == 0) {
        if (!cm4_transport_validate_invalidation_request(&request) ||
            !cm4_transport_format_invalidation_ack(&request, expected_ack,
                                                   sizeof(expected_ack), &expected_ack_length)) {
            fputs("control request was rejected\n", errors);
            return 2;
        }
    } else {
        fputs("control request was rejected\n", errors);
        return 2;
    }

    if (!exchange(exchange_context, request_line, request_length,
                  response, sizeof(response), &response_length) ||
        response_length != expected_ack_length ||
        memcmp(response, expected_ack, expected_ack_length) != 0) {
        fputs("control server did not confirm the current target\n", errors);
        return 1;
    }

    if (fwrite(response, 1, response_length, output) != response_length ||
        fputc('\n', output) == EOF || fflush(output) != 0) {
        fputs("control acknowledgement could not be written\n", errors);
        return 1;
    }
    return 0;
}
