#include "transport_server_posix.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static int copy_argument(char *destination, size_t capacity, const char *source)
{
    size_t length = strlen(source);
    if (length == 0 || length >= capacity) return 0;
    memcpy(destination, source, length + 1);
    return 1;
}

static int parse_generation(const char *text, uint64_t *generation)
{
    char *end = NULL;
    unsigned long long value;
    if (text == NULL || *text == '\0' || text[0] == '-') return 0;
    value = strtoull(text, &end, 10);
    if (end == text || *end != '\0' || value == 0) return 0;
    *generation = (uint64_t)value;
    return 1;
}

static int run_server_from_arguments(int argc, char **argv)
{
    cm4_transport_identity cm4;
    char helper_launch_id[CM4_TRANSPORT_TEXT_CAPACITY] = { 0 };
    char cm4_ipv4_text[CM4_TRANSPORT_IPV4_TEXT_CAPACITY] = { 0 };
    uint8_t cm4_ipv4[4];
    unsigned int seen = 0;
    int index;

    memset(&cm4, 0, sizeof(cm4));
    for (index = 2; index < argc; index += 2) {
        const char *key = argv[index];
        const char *value;
        unsigned int bit;
        if (index + 1 >= argc) return 2;
        value = argv[index + 1];
        if (strcmp(key, "--run-id") == 0) {
            bit = 1u << 0;
            if (!copy_argument(cm4.run_id, sizeof(cm4.run_id), value)) return 2;
        } else if (strcmp(key, "--engine-id") == 0) {
            bit = 1u << 1;
            if (!copy_argument(cm4.engine_id, sizeof(cm4.engine_id), value)) return 2;
        } else if (strcmp(key, "--cm4-resource-id") == 0) {
            bit = 1u << 2;
            if (!copy_argument(cm4.resource_id, sizeof(cm4.resource_id), value)) return 2;
        } else if (strcmp(key, "--cm4-container-id") == 0) {
            bit = 1u << 3;
            if (!copy_argument(cm4.container_id, sizeof(cm4.container_id), value)) return 2;
        } else if (strcmp(key, "--cm4-generation") == 0) {
            bit = 1u << 4;
            if (!parse_generation(value, &cm4.generation)) return 2;
        } else if (strcmp(key, "--helper-launch-id") == 0) {
            bit = 1u << 5;
            if (!copy_argument(helper_launch_id, sizeof(helper_launch_id), value)) return 2;
        } else if (strcmp(key, "--cm4-ip") == 0) {
            bit = 1u << 6;
            if (!copy_argument(cm4_ipv4_text, sizeof(cm4_ipv4_text), value)) return 2;
        } else {
            return 2;
        }
        if ((seen & bit) != 0) return 2;
        seen |= bit;
    }

    if (seen != 0x7fu || !cm4_transport_parse_ipv4_address(cm4_ipv4_text, cm4_ipv4)) return 2;
    return cm4_transport_run_server(&cm4, helper_launch_id, cm4_ipv4);
}

int main(int argc, char **argv)
{
    if (argc == 3 && strcmp(argv[1], "--protocol") == 0 &&
        strcmp(argv[2], "jsonl-v1") == 0) {
        int result;
        alarm(3);
        result = cm4_transport_run_control_cli();
        alarm(0);
        return result;
    }
    if (argc >= 2 && strcmp(argv[1], "--server") == 0) {
        return run_server_from_arguments(argc, argv);
    }
    fputs("expected --protocol jsonl-v1 or explicit --server arguments\n", stderr);
    return 2;
}
