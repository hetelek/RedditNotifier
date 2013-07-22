using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RedditSharp;
using System.Security.Authentication;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;

namespace RedditNotifier
{
    class Program
    {
        [Flags]
        enum RedditAction
        {
            Message = 1,
            Save = 2,
            Upvote = 3,
            Downvote = 4,
            Approve = 5
        };

        static RedditAction redditActions = 0;
        static Reddit reddit = new Reddit();
        static int checkInterval = 0;
        static string[] subredditNames;
        static string recipientUsername;
        static long[] lastCreatedTicks;
        static Regex regex;

        static void Main(string[] args)
        {
            try
            {
                while (reddit.User == null)
                {
                    Console.Write("Username: ");
                    string username = Console.ReadLine();

                    Console.Write("Password: ");
                    string password = ReadPassword();

                    try
                    {
                        reddit.LogIn(username, password);
                    }
                    catch { }
                }
                recipientUsername = reddit.GetMe().FullName;

                Console.WriteLine("Available actions:");
                Console.WriteLine("[1] Send a message to myself");
                Console.WriteLine("[2] Save it to my saved posts");
                Console.WriteLine("[3] Upvote it");
                Console.WriteLine("[4] Downvote it");
                Console.WriteLine("[5] Approve it");
                Console.WriteLine();
                Console.Write("What actions should be taken when a match is found (seperate multiple with commas)?: ");

                bool validInterval;
                string[] actionStrings = Console.ReadLine().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < actionStrings.Length; i++)
                {
                    RedditAction currentAction;
                    validInterval = Enum.TryParse<RedditAction>(actionStrings[i], out currentAction);
                    if (validInterval)
                        redditActions |= currentAction;

                    if (!validInterval || (redditActions.HasFlag(RedditAction.Downvote) && redditActions.HasFlag(RedditAction.Upvote)))
                    {
                        Console.WriteLine("Setting actions as default...");
                        redditActions = RedditAction.Save;
                    }
                }

                Console.Write("Check interval (seconds): ");
                validInterval = Int32.TryParse(Console.ReadLine(), out checkInterval);
                if (!validInterval)
                {
                    Console.WriteLine("Setting check interval as default...");
                    checkInterval = 300;
                }

                Console.Write("Subreddits (seperate w/ comma): ");
                subredditNames = Console.ReadLine().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (subredditNames.Length == 0)
                {
                    Console.WriteLine("Setting subreddit name as default...");
                    subredditNames = new string[] { "jailbreak" };
                }
                lastCreatedTicks = new long[subredditNames.Length];

                Console.Write("Regular expression: ");
                string regexStr = Console.ReadLine();
                if (regexStr == String.Empty)
                {
                    Console.WriteLine("Setting regex as default...");
                    regexStr = @"\[(Tweak Idea|Idea|Request|Tweak Request|)\]|Is there a tweak";
                }

                regex = new Regex(regexStr, RegexOptions.IgnoreCase);

                Console.WriteLine("Press a key at any time to exit.");
                Console.WriteLine("Starting...");

                CheckPosts(null);

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occured: " + ex.Message);
            }
        }

        static void CheckPosts(object state)
        {
            try
            {
                Console.WriteLine();

                StringBuilder messageBody = new StringBuilder();
                for (int i = 0; i < subredditNames.Length; i++)
                {
                    string subredditName = subredditNames[i];

                    Console.WriteLine("Checking new posts in /r/" + subredditName + "...");
                    Subreddit subreddit = reddit.GetSubreddit(subredditName);
                    Listing<Post> posts = subreddit.GetNew();

                    bool first = true;
                    int checkedCount = 0;
                    long newLastCreatedTicks = 0;
                    foreach (Post post in posts.Take(25))
                    {
                        if (post.Created.Ticks == lastCreatedTicks[i])
                        {
                            Console.WriteLine("Checked " + checkedCount + " new post(s).");
                            break;
                        }
                        else if (first)
                        {
                            newLastCreatedTicks = post.Created.Ticks;
                            first = false;
                        }

                        checkedCount++;
                        if (regex.IsMatch(post.Title))
                        {
                            if (redditActions.HasFlag(RedditAction.Save))
                                post.Save();
                            if (redditActions.HasFlag(RedditAction.Message))
                                messageBody.AppendLine("1. [" + post.Title + "](" + post.Shortlink + ")");
                            if (redditActions.HasFlag(RedditAction.Approve))
                                post.Approve();
                            if (redditActions.HasFlag(RedditAction.Upvote))
                                post.Upvote();
                            if (redditActions.HasFlag(RedditAction.Downvote))
                                post.Downvote();

                            Console.WriteLine("Found: " + post.Title);
                        }
                    }

                    if (newLastCreatedTicks != 0)
                        lastCreatedTicks[i] = newLastCreatedTicks;
                }

                if (redditActions.HasFlag(RedditAction.Message) && messageBody.Length > 0)
                    reddit.ComposePrivateMessage("reddit Match Found", messageBody.ToString(), recipientUsername);

                Console.WriteLine("Finished. Waiting " + checkInterval + " seconds...");
                new Timer(new TimerCallback(CheckPosts), null, checkInterval * 1000, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occured: " + ex.Message);
            }
        }

        public static string ReadPassword()
        {
            Stack<string> passbits = new Stack<string>();
            for (ConsoleKeyInfo cki = Console.ReadKey(true); cki.Key != ConsoleKey.Enter; cki = Console.ReadKey(true))
            {
                if (cki.Key == ConsoleKey.Backspace)
                {
                    if (passbits.Count == 0)
                        continue;

                    Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    Console.Write(" ");
                    Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    passbits.Pop();
                }
                else
                {
                    Console.Write("*");
                    passbits.Push(cki.KeyChar.ToString());
                }
            }
            string[] pass = passbits.ToArray();
            Array.Reverse(pass);
            Console.Write(Environment.NewLine);
            return string.Join(string.Empty, pass);
        }
    }
}
