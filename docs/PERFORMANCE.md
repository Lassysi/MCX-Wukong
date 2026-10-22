# Performance and streaming

English | [简体中文](PERFORMANCE.zh-CN.md)

Both games render at once. Start with Minecraft at 1280×720, render distance 6 and 60 FPS, no extra shader pack; keep it running unminimized. Allocate around 4 GB initially and leave memory for Black Myth and Windows. Use the same dedicated GPU for the bridge/capture path. Avoid moving Minecraft to an integrated GPU as an untested workaround.

Cap Black Myth at 60 FPS first, lower graphics or try 2560×1440 if GPU saturation persists. These are starting points, not measured minimum requirements. After changing window size, confirm camera/HUD alignment.

For OBS Virtual Camera into Bilibili Livehime, start with a 1920×1080 OBS canvas/output at 30 FPS and matching Livehime output. Re-fit scene sources after changing canvas size. Final streaming encoding is configured in Livehime; choose NVIDIA hardware encoding if available. Stop unused OBS recording/streaming/replay, reduce unnecessary capture sources and close unneeded browser workloads. Preserve any Spout/composition source required for your actual bridge layout.

OBS recommends capping game FPS and simplifying scenes to leave GPU time for compositing: https://obsproject.com/kb/encoding-performance-troubleshooting

The bundled new-instance options disable tutorial hints (`tutorialStep:none`). Existing instances can change this in `options.txt` only after Minecraft is closed. This is the top-right tutorial toast, not an advancement; advancement announcements are separate.
