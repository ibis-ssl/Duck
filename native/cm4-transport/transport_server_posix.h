#ifndef DUCK_CM4_TRANSPORT_SERVER_POSIX_H
#define DUCK_CM4_TRANSPORT_SERVER_POSIX_H

#include "transport_core.h"

int cm4_transport_run_server(
    const cm4_transport_identity *cm4,
    const char *helper_launch_id,
    const uint8_t cm4_ipv4[4]);

int cm4_transport_run_control_cli(void);

#endif
