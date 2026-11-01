#ifndef DUCK_CM4_TRANSPORT_PATH_POLICY_H
#define DUCK_CM4_TRANSPORT_PATH_POLICY_H

#include <stdint.h>

typedef enum cm4_transport_path_kind {
    CM4_TRANSPORT_PATH_OTHER = 0,
    CM4_TRANSPORT_PATH_DIRECTORY = 1,
    CM4_TRANSPORT_PATH_SOCKET = 2,
    CM4_TRANSPORT_PATH_SYMLINK = 3
} cm4_transport_path_kind;

typedef struct cm4_transport_path_identity {
    uint64_t device;
    uint64_t inode;
    uint64_t owner;
    uint32_t mode;
    cm4_transport_path_kind kind;
} cm4_transport_path_identity;

typedef void (*cm4_transport_owned_path_action_fn)(void *context);

int cm4_transport_owned_directory_matches(
    const cm4_transport_path_identity *identity,
    uint64_t effective_owner);

int cm4_transport_owned_socket_matches(
    const cm4_transport_path_identity *identity,
    uint64_t effective_owner);

int cm4_transport_socket_endpoint_matches(
    const cm4_transport_path_identity *directory_before,
    const cm4_transport_path_identity *directory_after,
    const cm4_transport_path_identity *socket_before,
    const cm4_transport_path_identity *socket_after,
    uint64_t peer_owner,
    uint64_t effective_owner);

int cm4_transport_can_unlink_owned_socket(
    const cm4_transport_path_identity *owned,
    const cm4_transport_path_identity *current);

int cm4_transport_can_remove_owned_directory(
    int created_by_process,
    const cm4_transport_path_identity *owned,
    const cm4_transport_path_identity *current);

void cm4_transport_cleanup_owned_paths(
    int socket_was_bound,
    int directory_was_created,
    const cm4_transport_path_identity *owned_socket,
    const cm4_transport_path_identity *current_socket,
    const cm4_transport_path_identity *owned_directory,
    const cm4_transport_path_identity *current_directory,
    cm4_transport_owned_path_action_fn unlink_socket,
    cm4_transport_owned_path_action_fn remove_directory,
    void *context);

uint32_t cm4_transport_control_socket_umask(void);
int cm4_transport_control_socket_mode_is_private(uint32_t mode);

#endif
