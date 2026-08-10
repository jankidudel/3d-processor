

class SomeFile
{
    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            Console.WriteLine($"Arguments passed: {string.Join(", ", args)}");
        }
        else
        {
            Console.WriteLine("No arguments were passed.");
        }


        Console.WriteLine("Hello, World!");

    }
}