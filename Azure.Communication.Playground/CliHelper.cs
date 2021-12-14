using System;

namespace Azure.Communication.Playground
{
    internal class CliHelper
    {
        public static T GetEnumFromCLI<T>(T defVal = default) where T : struct, Enum
        {
            T value = defVal;
            Console.WriteLine($"Specify the {value.GetType().Name}: ");
            foreach (var item in Enum.GetValues(typeof(T)))
            {
                string defString = ((int)item) == Convert.ToInt32(defVal) ? " (default)" : "";
                Console.WriteLine($"\t- {item}: {(int)item}{defString}");
            }
            var succ = Enum.TryParse(Console.ReadLine(), out value);
            return succ ? value : defVal;
        }
    }
}
