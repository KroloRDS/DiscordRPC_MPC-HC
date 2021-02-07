# About
Windows service that lets people on Discord know what you are currently watching in MPC-HC. It detecs running MPC-HC instance, reads metadata and displays it using Discord Rich Presence.

Technologies used:
* C#
* Discord Rich Presence API

# How to install?

1. Clone the repo
2. Compile
3. Open "Developer Commend Prompt for VS" with admin privilages
4. Type `cd [path_to_repo]/bin/Debug`
5. Type `installutil MPC-HC_DiscordRPC_Service.exe`
6. Open Services Manager and run MPC-HP DiscordRPC Service
7. Open MPC-HC
8. Go to View -> Options -> Player -> Web Interface
9. Make sure "Listen on port" is checked and port numer is 13579
