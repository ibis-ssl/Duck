#include "transport_path_policy.h"

int cm4_transport_owned_directory_matches(
    const cm4_transport_path_identity *identity,
    uint64_t effective_owner)
{
    return identity != 0 && identity->kind == CM4_TRANSPORT_PATH_DIRECTORY &&
        identity->owner == effective_owner && (identity->mode & 07777u) == 0700u;
}

int cm4_transport_owned_socket_matches(
    const cm4_transport_path_identity *identity,
    uint64_t effective_owner)
{
    return identity != 0 && identity->kind == CM4_TRANSPORT_PATH_SOCKET &&
        identity->owner == effective_owner && (identity->mode & 07777u) == 0600u;
}

int cm4_transport_socket_endpoint_matches(
    const cm4_transport_path_identity *directory_before,
    const cm4_transport_path_identity *directory_after,
    const cm4_transport_path_identity *socket_before,
    const cm4_transport_path_identity *socket_after,
    uint64_t peer_owner,
    uint64_t effective_owner)
{
    return cm4_transport_owned_directory_matches(directory_before, effective_owner) &&
        cm4_transport_owned_directory_matches(directory_after, effective_owner) &&
        directory_before->device == directory_after->device &&
        directory_before->inode == directory_after->inode &&
        cm4_transport_owned_socket_matches(socket_before, effective_owner) &&
        cm4_transport_owned_socket_matches(socket_after, effective_owner) &&
        socket_before->device == socket_after->device &&
        socket_before->inode == socket_after->inode &&
        peer_owner == effective_owner;
}

int cm4_transport_can_unlink_owned_socket(
    const cm4_transport_path_identity *owned,
    const cm4_transport_path_identity *current)
{
    return owned != 0 && current != 0 && owned->kind == CM4_TRANSPORT_PATH_SOCKET &&
        current->kind == CM4_TRANSPORT_PATH_SOCKET && owned->device == current->device &&
        owned->inode == current->inode && owned->owner == current->owner;
}

int cm4_transport_can_remove_owned_directory(
    int created_by_process,
    const cm4_transport_path_identity *owned,
    const cm4_transport_path_identity *current)
{
    return created_by_process && owned != 0 && current != 0 &&
        owned->kind == CM4_TRANSPORT_PATH_DIRECTORY &&
        current->kind == CM4_TRANSPORT_PATH_DIRECTORY &&
        owned->device == current->device && owned->inode == current->inode &&
        owned->owner == current->owner && owned->mode == current->mode &&
        (current->mode & 07777u) == 0700u;
}

void cm4_transport_cleanup_owned_paths(
    int socket_was_bound,
    int directory_was_created,
    const cm4_transport_path_identity *owned_socket,
    const cm4_transport_path_identity *current_socket,
    const cm4_transport_path_identity *owned_directory,
    const cm4_transport_path_identity *current_directory,
    cm4_transport_owned_path_action_fn unlink_socket,
    cm4_transport_owned_path_action_fn remove_directory,
    void *context)
{
    if (socket_was_bound && unlink_socket != 0 &&
        cm4_transport_can_unlink_owned_socket(owned_socket, current_socket)) {
        unlink_socket(context);
    }
    if (directory_was_created && remove_directory != 0 &&
        cm4_transport_can_remove_owned_directory(1, owned_directory, current_directory)) {
        remove_directory(context);
    }
}

uint32_t cm4_transport_control_socket_umask(void)
{
    return 0177;
}

int cm4_transport_control_socket_mode_is_private(uint32_t mode)
{
    return (mode & 07777u) == 0600u;
}
