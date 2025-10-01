namespace RootedLauncher
{
    public static class RootedMcAnimation
    {
        private static readonly string[][] Frames = new string[][]
        {
            // Frame 1 - R
            new string[]
            {
                "██████╗                                                              ",
                "██╔══██╗                                                             ",
                "██████╔╝                                                             ",
                "██╔══██╗                                                             ",
                "██║  ██║                                                             ",
                "╚═╝  ╚═╝                                                             "
            },
            // Frame 2 - RO
            new string[]
            {
                "██████╗  ██████╗                                                     ",
                "██╔══██╗██╔═══██╗                                                    ",
                "██████╔╝██║   ██║                                                    ",
                "██╔══██╗██║   ██║                                                    ",
                "██║  ██║╚██████╔╝                                                    ",
                "╚═╝  ╚═╝ ╚═════╝                                                     "
            },
            // Frame 3 - ROO
            new string[]
            {
                "██████╗  ██████╗  ██████╗                                           ",
                "██╔══██╗██╔═══██╗██╔═══██╗                                          ",
                "██████╔╝██║   ██║██║   ██║                                          ",
                "██╔══██╗██║   ██║██║   ██║                                          ",
                "██║  ██║╚██████╔╝╚██████╔╝                                          ",
                "╚═╝  ╚═╝ ╚═════╝  ╚═════╝                                           "
            },
            // Frame 4 - ROOT
            new string[]
            {
                "██████╗  ██████╗  ██████╗ ████████╗                                ",
                "██╔══██╗██╔═══██╗██╔═══██╗╚══██╔══╝                                ",
                "██████╔╝██║   ██║██║   ██║   ██║                                   ",
                "██╔══██╗██║   ██║██║   ██║   ██║                                   ",
                "██║  ██║╚██████╔╝╚██████╔╝   ██║                                   ",
                "╚═╝  ╚═╝ ╚═════╝  ╚═════╝    ╚═╝                                   "
            },
            // Frame 5 - ROOTE
            new string[]
            {
                "██████╗  ██████╗  ██████╗ ████████╗███████╗                        ",
                "██╔══██╗██╔═══██╗██╔═══██╗╚══██╔══╝██╔════╝                        ",
                "██████╔╝██║   ██║██║   ██║   ██║   █████╗                          ",
                "██╔══██╗██║   ██║██║   ██║   ██║   ██╔══╝                          ",
                "██║  ██║╚██████╔╝╚██████╔╝   ██║   ███████╗                        ",
                "╚═╝  ╚═╝ ╚═════╝  ╚═════╝    ╚═╝   ╚══════╝                        "
            },
            // Frame 6 - ROOTED
            new string[]
            {
                "██████╗  ██████╗  ██████╗ ████████╗███████╗██████╗                 ",
                "██╔══██╗██╔═══██╗██╔═══██╗╚══██╔══╝██╔════╝██╔══██╗                ",
                "██████╔╝██║   ██║██║   ██║   ██║   █████╗  ██║  ██║                ",
                "██╔══██╗██║   ██║██║   ██║   ██║   ██╔══╝  ██║  ██║                ",
                "██║  ██║╚██████╔╝╚██████╔╝   ██║   ███████╗██████╔╝                ",
                "╚═╝  ╚═╝ ╚═════╝  ╚═════╝    ╚═╝   ╚══════╝╚═════╝                 "
            },
            // Frame 7 - ROOTEDM
            new string[]
            {
                "██████╗  ██████╗  ██████╗ ████████╗███████╗██████╗ ███╗   ███╗    ",
                "██╔══██╗██╔═══██╗██╔═══██╗╚══██╔══╝██╔════╝██╔══██╗████╗ ████║    ",
                "██████╔╝██║   ██║██║   ██║   ██║   █████╗  ██║  ██║██╔████╔██║    ",
                "██╔══██╗██║   ██║██║   ██║   ██║   ██╔══╝  ██║  ██║██║╚██╔╝██║    ",
                "██║  ██║╚██████╔╝╚██████╔╝   ██║   ███████╗██████╔╝██║ ╚═╝ ██║    ",
                "╚═╝  ╚═╝ ╚═════╝  ╚═════╝    ╚═╝   ╚══════╝╚═════╝ ╚═╝     ╚═╝    "
            },
            // Frame 8 - ROOTEDMC (Complete)
            new string[]
            {
                "██████╗  ██████╗  ██████╗ ████████╗███████╗██████╗ ███╗   ███╗ ██████╗",
                "██╔══██╗██╔═══██╗██╔═══██╗╚══██╔══╝██╔════╝██╔══██╗████╗ ████║██╔════╝",
                "██████╔╝██║   ██║██║   ██║   ██║   █████╗  ██║  ██║██╔████╔██║██║     ",
                "██╔══██╗██║   ██║██║   ██║   ██║   ██╔══╝  ██║  ██║██║╚██╔╝██║██║     ",
                "██║  ██║╚██████╔╝╚██████╔╝   ██║   ███████╗██████╔╝██║ ╚═╝ ██║╚██████╗",
                "╚═╝  ╚═╝ ╚═════╝  ╚═════╝    ╚═╝   ╚══════╝╚═════╝ ╚═╝     ╚═╝ ╚═════╝"
            }
        };

        /// <summary>
        /// Plays the RootedMc logo animation with frame-by-frame drawing effect
        /// </summary>
        /// <param name="frameDelay">Delay between frames in milliseconds (default: 200)</param>
        /// <param name="finalPause">Pause after animation completes in milliseconds (default: 500)</param>
        /// <param name="color">Console color for the logo (default: Green)</param>
        public static async Task Play(int frameDelay = 200, int finalPause = 500, ConsoleColor color = ConsoleColor.Green)
        {
            Console.CursorVisible = false;
            int startLine = Console.CursorTop;

            foreach (var frame in Frames)
            {
                Console.SetCursorPosition(0, startLine);
                Console.ForegroundColor = color;

                foreach (var line in frame)
                {
                    Console.WriteLine(line);
                }

                await Task.Delay(frameDelay);
            }

            Console.ResetColor();
            Console.CursorVisible = true;
            await Task.Delay(finalPause);
        }
    }
}

// USAGE EXAMPLES:
// 
// Basic usage:
// await RootedMcAnimation.Play();
//
// Custom speed (faster):
// await RootedMcAnimation.Play(frameDelay: 100);
//
// Custom color:
// await RootedMcAnimation.Play(color: ConsoleColor.Cyan);
//
// Full customization:
// await RootedMcAnimation.Play(frameDelay: 150, finalPause: 1000, color: ConsoleColor.Yellow);