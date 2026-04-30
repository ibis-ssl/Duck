# SSL_WrapperPacket Raw Vision Viewer Plan

## Goal

Tracker.Server receives raw SSL-Vision `SSL_WrapperPacket` datagrams directly from `SslProto` generated types and renders the latest detection and geometry data in the root Blazor page.

## Scope

- Add a UDP hosted service that binds to the configured vision endpoint.
- Join the multicast group when the configured address is multicast.
- Decode packets with `SSL_WrapperPacket.Parser.ParseFrom`.
- Store the latest packet, detection frame, geometry data, receive metadata, packet count, and error count in a singleton store.
- Render a field SVG, detection tables, geometry calibration table, and raw packet JSON on `/`.
- Keep the navigation focused on the raw vision viewer.

## Non-Goals

- Do not use `TrackerConnectionLib`.
- Do not persist packets.
- Do not add robot tracking, filtering, or world-model interpretation beyond raw detection rendering.

## Configuration

`appsettings.json` contains a `VisionReceiver` section:

- `MulticastAddress`: default `224.5.23.2`.
- `Port`: default `10006`.
- `InterfaceAddress`: optional local interface address used for multicast joins.

## Receiver Design

`VisionReceiverService` runs as a hosted background service. It creates an IPv4 UDP socket, enables address reuse, binds to `IPAddress.Any` on the configured port, optionally joins the configured multicast group, and continuously receives datagrams until cancellation.

Each datagram is decoded into `SSL_WrapperPacket`. Successful decodes update `VisionPacketStore` with cloned packet data and receive metadata. Failed decodes increment the error count and retain the previous successful packet.

## Store Design

`VisionPacketStore` owns thread-safe state for UI reads:

- latest wrapper packet
- latest detection frame
- latest geometry data
- packet count
- error count
- remote endpoint
- receive timestamp
- latest parse error message

The store exposes immutable snapshots so Blazor components can render without holding locks.

## Field Projection

`VisionFieldProjection` maps SSL field millimeter coordinates to an SVG viewport. It uses geometry field dimensions when available and falls back to regulation-style defaults before any geometry packet is received. `(0, 0)` maps to the viewport center, `x` increases right, and `y` increases upward in field coordinates.

## UI

The root page renders:

- receiver status and latest receive metadata
- SVG field with geometry lines/arcs and raw balls/robots
- tables for balls, yellow robots, blue robots, and camera calibration
- raw JSON generated with `Google.Protobuf.JsonFormatter`

## Test Plan

- `VisionPacketStore` stores detection-only packets.
- `VisionPacketStore` stores geometry-only packets.
- `VisionPacketStore` increments error count after decode failures.
- `VisionFieldProjection` maps `(0, 0)` to the SVG center.
- `VisionFieldProjection` keeps field length/width endpoints inside the viewport.
- `VisionFieldProjection` uses configured defaults when geometry has not been received.

## Assumptions

- "Raw vision information" means `vision/ssl_vision_wrapper.proto` `SSL_WrapperPacket`.
- The default SSL-Vision multicast endpoint is `224.5.23.2:10006`.
- Existing unrelated worktree changes are preserved.
