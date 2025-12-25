using System;
using System.Collections.Generic;

namespace Hazina.Tools.Services.Chat
{
    /// <summary>
    /// Provides random opening questions for business idea interview chats.
    /// </summary>
    public static class OpeningQuestions
    {
        private static readonly Random _random = new Random();

        private static readonly List<string> _questions = new List<string>
        {
            "What problem have you noticed in the world that really bothers you, and you think you could solve?",

            "If you could wave a magic wand and make one thing easier for a specific group of people, what would it be and who would benefit?",

            "Tell me about a frustrating experience you or someone you know had recently that made you think 'there has to be a better way to do this'?",

            "What's an idea that's been bouncing around in your head that you can't seem to shake?",

            "If money and resources weren't a constraint, what business would you start tomorrow?",

            "What's something you're uniquely positioned to build or offer that most people couldn't?",

            "Describe a moment when you thought 'why doesn't this exist yet?' - what were you looking for?",

            "What industry or market do you have insider knowledge about, and what opportunity do you see there that others might be missing?",

            "If you could go back in time and start a business to solve a problem you faced 5 years ago, what would it be?",

            "What's a service or product you currently pay for that you think could be 10x better?",

            "Tell me about your background and expertise - what could you teach or build that people would pay for?",

            "What change in the world (technology, regulation, behavior) creates an opportunity that excites you?",

            "If you had to start earning money from a new business in the next 90 days, what would you create?",

            "What's something people constantly ask you for help with or advice about?",

            "Describe your ideal workday 5 years from now - what business would enable that lifestyle?",

            "What's a product or service you use regularly and think 'I could do this better'?",

            "If you could combine your passion with solving someone's problem, what would that look like?",

            "What gap in the market have you noticed where demand exists but supply is inadequate?",

            "Tell me about a 'jobs to be done' moment - when did you hire a product/service to accomplish something and it fell short?",

            "What business idea scares you a little bit because it feels too ambitious, but you can't stop thinking about it?"
        };

        /// <summary>
        /// Gets a random opening question for starting a business idea interview.
        /// </summary>
        public static string GetRandomQuestion()
        {
            int index = _random.Next(_questions.Count);
            return _questions[index];
        }

        /// <summary>
        /// Gets all opening questions.
        /// </summary>
        public static IReadOnlyList<string> GetAllQuestions()
        {
            return _questions.AsReadOnly();
        }
    }
}
