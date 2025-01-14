using MalignEngine;
using System.Numerics;

namespace SnakeGame;

public static class MessageExtensions
{
    public static void WriteCharArray(this IWriteMessage message, string str)
    {
        if (str.Length >= byte.MaxValue )
        {
            throw new Exception("String too long");
        }

        message.WriteByte((byte)str.Length);

        for (int i = 0; i < str.Length; i++)
        {
            message.WriteByte((byte)str[i]);
        }

        message.WriteByte(0);
    }

    public static string ReadCharArray(this IReadMessage message)
    {
        byte length = message.ReadByte();
        char[] chars = new char[length];

        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)message.ReadByte();
        }

        message.ReadByte();

        return new string(chars);
    }
}