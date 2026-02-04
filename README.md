# MirrorRelayJesus
UDP multi client relay with host registry and limiters, for Mirror Netcode.


# Script Structure:
// RelayGame.cs
- Runs on a host/server instance.
- Responsible for Configuring the game transport port
- Registering the host with the relay
- Sending signed keep alive heartbeats
- Notifying the relay when player count or other settings change

// RelayServer.cs
- UDP relay that accepts client connections
- Routes clients to registered hosts
- Applies rate limiting, cooldowns, and abuse prevention
- Provides stats for a live (development/editor) dashboard

// RelayServerUI.cs
- Visual debug dashboard for the UDP relay.
- Displays relay state, hosts, clients, and security counters.
- Intended for development & diagnostics only.

// RelayKcpTransport.cs
- Custom KCP transport that routes client connections through a UDP relay.
- Optionally sends a lightweight authentication token to the relay before initiating the KCP connection.

// RelaySettings.cs RelaySettingsGame.cs RelaySettingsShared.cs
- Easily accessable static variables and functions.
- Split into 3 files, to prevent setting duplication and leaking relay settings.
- Relay uses RelaySettings and RelaySettingsShared.
- Players uses RelaySettingsGame and RelaySettingsShared.


# Development Notes:
- Settings variables! Separated into 3 files, Relay, Game and Shared - reasoning, we do not want to ship relay-specific settings to users, and we do not want to duplicate settings for sanity.
- Settings and Logs now statics, to be accessed without inspector references, and from anywhere.

- Separate player unity scene, originally was using a mirror example, not suitable long term for upgrading or overwriting mirror versions.
- Relay scene has temporary camera, as unity editors 'no camera detected' message is annoyingly covering up development ui.

- Old draft files and scenes fixed to work again, allows referencing for newer refactor.
- Trimmer for dictionary sizes.
- Host register data now signed and encrypted
- NetworkManager now handles register/heartbeat for host servers.


