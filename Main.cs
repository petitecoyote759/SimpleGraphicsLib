using Silk.NET;
using Silk.NET.SDL;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;





namespace SimpleGraphicsLib
{
    [Flags]
    public enum RendererFlag
    {
        /// <summary>
        /// Makes the program output to the terminal. Overwrites the log function if that was passed into the constructor.
        /// </summary>
        OutputToTerminal,
        HalfSizedWindow
    }

    public class GraphicsHandler: IDisposable
    {
        public int screenwidth = -1;
        public int screenheight = -1;

        private System.Threading.Thread? thread = null;
        /// <summary>
        /// Boolean storing if the program is running. If it is false then the program will pause until it is true.
        /// </summary>
        private bool running = true;
        /// <inheritdoc cref="running"/>
        public bool Running { get => running; }
        /// <summary>
        /// The time in milliseconds when the thread should wake up
        /// </summary>
        private long sleepTime;


        private readonly RendererFlag[] flags = Array.Empty<RendererFlag>();

        /// <summary>
        /// Function to output log info
        /// </summary>
        private Action<string> logOutput;


        private bool disposed = false;



        private Sdl sdl;

        private unsafe Window* window;
        private unsafe Renderer* renderer;

        Action render;


        private byte[] pixelBuffer;
        private unsafe Texture* texture;






        /// <summary>
        /// Class for creating a window and drawing simple designs.
        /// </summary>
        /// <param name="screenWidth"></param>
        /// <param name="screenHeight"></param>
        /// <param name="render"></param>
        /// <param name="logOutput"></param>
        /// <param name="flags"></param>
        public GraphicsHandler(
            int screenWidth = -1, int screenHeight = -1,
            Action? render = null, Action<string>? logOutput = null, 
            params RendererFlag[] flags)
        {


            this.render = render ?? (() => { });

            this.flags = flags;

            if (flags.Contains(RendererFlag.OutputToTerminal))
            {
                this.logOutput = Console.WriteLine;
            }
            else
            {
                this.logOutput = logOutput ?? ((string _) => { });
            }




            try
            {
                Start(screenWidth, screenHeight);
            }
            catch (Exception e)
            {
                this.logOutput("Ruh roh");
            }
        }



        private ManualResetEventSlim setupDone = new(false);
        private unsafe void Setup(int screenWidth, int screenHeight)
        {
            // Create SDL + Image contexts
            sdl = Sdl.GetApi();

            // SDL Init
            if (sdl.Init(Sdl.InitEverything | Sdl.InitSensor) < 0)
            {
                logOutput("SDL couldn't initialise, reason = " + sdl.GetErrorS());
            }





            if ((screenwidth != -1 && screenheight != -1) || flags.Contains(RendererFlag.HalfSizedWindow))
            {
                // Get display mode
                DisplayMode displayMode;

                if (sdl.GetCurrentDisplayMode(0, &displayMode) < 0)
                {
                    // Fallback
                    if (sdl.GetDesktopDisplayMode(0, &displayMode) < 0)
                    {
                        sdl.Quit();
                        logOutput(
                            "SDL couldn't initialise either display mode, reason = " +
                            sdl.GetErrorS()
                        );
                    }
                }

                if (flags.Contains(RendererFlag.HalfSizedWindow))
                {
                    screenwidth = displayMode.W / 2;
                    screenheight = displayMode.H / 2;
                }
                else
                {
                    screenwidth = displayMode.W;
                    screenheight = displayMode.H;
                }
            }
            else
            {
                screenwidth = screenWidth;
                screenheight = screenHeight;
            }


            




            // Create window (borderless, centered)
            window = sdl.CreateWindow(
                    "SGL Window",
                    Sdl.WindowposCentered,
                    Sdl.WindowposCentered,
                    screenwidth,
                    screenheight,
                    (uint)WindowFlags.Borderless
                );

            if (window == null)
            {
                sdl.Quit();
                logOutput("SDL window couldn't initialise, reason = " + sdl.GetErrorS());
            }

            // Create renderer (accelerated + vsync)
            renderer = sdl.CreateRenderer(
                window,
                -1,
                (uint)(RendererFlags.Accelerated)
            );

            if (renderer == null)
            {
                sdl.Quit();
                logOutput("SDL renderer couldn't initialise, reason = " + sdl.GetErrorS());
            }

            // Set default draw color (white)
            if (sdl.SetRenderDrawColor(renderer, 255, 255, 255, 255) < 0)
            {
                sdl.Quit();
                logOutput("SDL draw colour couldn't initialise, reason = " + sdl.GetErrorS());
            }



            pixelBuffer = new byte[screenwidth * screenheight * 4];

            texture = sdl.CreateTexture(
                renderer,
                (uint)PixelFormatEnum.Rgb888,
                (int)TextureAccess.Streaming,
                screenwidth,
                screenheight
            );



            setupDone.Set();
        }

















        /// <summary>
        /// Pauses the renderer thread, restart it with Resume or pass in a time for it to sleep for.
        /// </summary>
        /// <param name="sleepTime">The time it will sleep for, if it is set to -1, it will wait until resume is called.</param>
        public void Pause(long sleepTime = -1)
        {
            this.sleepTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + sleepTime;
            logOutput("Thread paused.");
            running = false;
        }
        /// <summary>
        /// Resumes the thread that runs the renderer after Pause has been called.
        /// </summary>
        public void Resume()
        {
            running = true;
        }



        /// <summary>
        /// Starts the renderer, only use this once when you want the renderer to start up for the first time. For stopping and starting the renderer see Pause() and Resume().
        /// </summary>
        /// <exception cref="SDLError">thrown when SDL errors.</exception>
        public void Start(int screenWidth, int screenHeight)
        {
            if (disposed)
            {
                logOutput($"Attempted to start a disposed renderer");
                return;
            }


            thread = new System.Threading.Thread(
                new ThreadStart(
                    (Action)(() => Setup(screenWidth, screenHeight)) + (Action)(() => Controller())
                    ));
            thread.Start();

            logOutput("Renderer started");

            setupDone.Wait();
        }








        unsafe void Controller()
        {
            while (!disposed)
            {
                lock (this)
                {
                    if (running)
                    {
                        render();


                        fixed (byte* ptr = &pixelBuffer[0])
                        {
                            // Upload CPU buffer to GPU texture
                            sdl.UpdateTexture(texture, null, ptr, screenwidth * 4);

                            // Render texture in one call
                            sdl.RenderCopy(renderer, texture, null, null);
                        }


                        sdl.RenderPresent(renderer);
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(20);
                    }
                }
            }
            ThreadDispose();


            logOutput("Renderer Stopped");
        }







        /// <summary>
        /// Sets a pixel range's colour.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>
        public unsafe void SetPixel(int x, int y, int w, int h, byte r, byte g, byte b, byte a = 255)
        {
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    int px = x + i;
                    int py = y + j;

                    if (px < 0 || px >= screenwidth || py < 0 || py >= screenheight)
                        continue; // ignore any outside

                    int idx = (py * screenwidth + px) * 4;
                    pixelBuffer[idx + 0] = r;
                    pixelBuffer[idx + 1] = g;
                    pixelBuffer[idx + 2] = b;
                    //pixelBuffer[idx + 3] = a;
                }
            }
        }






        void IDisposable.Dispose()
        {
            disposed = true;
            GC.SuppressFinalize(this);
        }


        unsafe void ThreadDispose()
        {
            lock (this)
            {
                
                sdl.DestroyRenderer(renderer);
                sdl.DestroyWindow(window);
                sdl.Dispose();
            }
        }
    }
}
