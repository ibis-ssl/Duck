#ifndef DUCK_CM4_TRANSPORT_CONTROL_FRONTEND_H
#define DUCK_CM4_TRANSPORT_CONTROL_FRONTEND_H

#include <stddef.h>
#include <stdio.h>

typedef int (*cm4_transport_exchange_fn)(
    void *context,
    const char *request,
    size_t request_length,
    char *response,
    size_t response_capacity,
    size_t *response_length);

int cm4_transport_run_control_stdio(
    FILE *input,
    FILE *output,
    FILE *errors,
    cm4_transport_exchange_fn exchange,
    void *exchange_context);

#endif
