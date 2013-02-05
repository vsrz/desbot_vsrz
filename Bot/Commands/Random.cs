using System;
namespace desBot
{
    /// <summary>
    /// Command that generates a random number
    /// </summary>
    class RandomCommand : Command
    {
        public static void AutoRegister()
        {
            new RandomCommand();
        }

        RandomCommand()
        {
            Privilege = PrivilegeLevel.OnChannel;
        }

        public override string GetKeyword()
        {
            return "random";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " (<min> <max>)|(<max>): Generates a random number between min and max, inclusive. If min is not specified, 1 is assumed. If max is not specified, 100 is assumed";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (Limiter.AttemptOperation(message.Level))
            {
                string[] arg = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int min = 1;
                int max = 100;
                if (arg.Length == 1)
                {
                    //get max
                    int.TryParse(arg[0], out max);
                }
                else if (arg.Length >= 2)
                {
                    //get min and max
                    int.TryParse(arg[0], out min);
                    int.TryParse(arg[1], out max);
                }
                if (min > max)
                {
                    //swap if max > min
                    int temp = min;
                    min = max;
                    max = temp;
                }

                //generate number
                int diff = max - min;
                int delta = diff != 0 ? new System.Random().Next(diff) : 0;
                int result = min + delta;

                //output result
                message.ReplyAuto("Random number between " + min.ToString() + " and " + max.ToString() + ": " + ControlCharacter.Underline() + result.ToString());
            }
        }
    }
}