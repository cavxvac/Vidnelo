# Third-party tools

Vidnelo is an independent graphical client. It runs the following tools as separate local processes. Their authors retain their respective rights and licenses; Vidnelo's MIT license does not replace those terms.

| Project | Purpose | Source and license information |
| --- | --- | --- |
| yt-dlp | Media extraction and downloading | [Project](https://github.com/yt-dlp/yt-dlp) · [Licensing](https://github.com/yt-dlp/yt-dlp#license) |
| FFmpeg / FFprobe | Media conversion, merging, and inspection | [FFmpeg](https://ffmpeg.org/) · [Build repository](https://github.com/yt-dlp/FFmpeg-Builds) · [FFmpeg license information](https://ffmpeg.org/legal.html) |
| Deno | JavaScript runtime used by yt-dlp | [Project](https://github.com/denoland/deno) · [License](https://github.com/denoland/deno/blob/main/LICENSE.md) |

Third-party executables are not included in this source repository or the small Windows release ZIP. First-time setup downloads them directly from the projects' GitHub release assets. The setup script records the asset URLs and SHA256 hashes in `tools/sources.txt`, and extracts the selected FFmpeg build's license to `tools/LICENSE.txt`.

The build selected by the setup script is `ffmpeg-master-latest-win64-gpl.zip`. Consult the linked projects for the terms applying to the particular downloaded distributions.

Vidnelo's branding artwork was generated with Imagegen. Artwork and the original prompts are included in `assets/`.
