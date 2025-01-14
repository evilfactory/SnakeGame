using MalignEngine;
using System.Numerics;
using System.Reflection;

namespace SnakeGame;

internal class Program
{
    static void Main(string[] args)
    {
        // Set working directory as where the .exe is located
        System.IO.Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        new GameMain();
    }
}