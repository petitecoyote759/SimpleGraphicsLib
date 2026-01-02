# SimpleGraphicsLibrary

This library is intended for small simple tests, such as visualising perlin noise. 

To create the renderer, create an instance of the GraphicsHandler, which can take the render action, screen dimensions, an action for outputting logs, and flags.

The most important part is the render action, which is what is constantly ran on the thread that is created. This program will create a window for 5 seconds and then it will close

	using (renderer = new GraphicsHandler(
            render: Render, 
            flags: [RendererFlag.OutputToTerminal, RendererFlag.HalfSizedWindow]))
        {
            Console.WriteLine("Testing");

            Thread.Sleep(5000);

            Console.WriteLine("Terminating");
        }


For an example render function, this is one to create a rainbow pattern.

    static int w = 32;
    static int h = 32;

    static readonly long start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
    static float offset = 2 * MathF.PI / 3f;
    private static void Render()
    {
        int xTiles = renderer.screenwidth / w;
        int yTiles = 1 + renderer.screenheight / h;

        for (int x = 0; x < xTiles; x++)
        {
            for (int y = 0; y < yTiles; y++)
            {
                float timeOffset = ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - start) / 100f) ;
                float number = (x + y) * 2 * MathF.PI / (yTiles + xTiles);
                byte r = (byte)((MathF.Cos(timeOffset + number) + 1) * (255f / 2));
                byte g = (byte)((MathF.Cos(timeOffset + number + offset) + 1) * (255f / 2));
                byte b = (byte)((MathF.Cos(timeOffset + number + 2 * offset) + 1) * (255f / 2));

                renderer.SetPixel(x * w, y * h, w, h, r, g, b);
            }
        }
    }