using Preflight.Constants;
using Preflight.Extensions;
using Preflight.Models;
using Preflight.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Preflight.Services
{
    public class ReadabilityService : IReadabilityService
    {
        ///  <summary>
        ///  the formula is: RE = 206.835 – (1.015 x ASL) – (84.6 x ASW) 
        ///  where RE = the readability ease
        ///  and ASL = average sentence length
        ///  and ASW = average syllables per word
        /// 
        ///  aiming for a score of 60-70
        /// 
        ///  /li, /ol, closing headings should be treated as sentences
        ///  strip all HTML tags
        ///  split on delimiters to count sentences
        ///  split on spaces to count words                   
        ///  count worth length 
        ///  split on vowels to count syllables and adjust for endings etc
        /// 
        ///  three or fewer letters are single syllable
        ///  </summary>
        ///  <param name="text">The string to parse</param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public ReadabilityResponseModel Check(string text, List<SettingsModel> settings)
        {
            var longWordLength = settings.GetValue<int>(KnownSettings.LongWordSyllables);
            var readabilityMin = settings.GetValue<int>(KnownSettings.ReadabilityMin);
            var readabilityMax = settings.GetValue<int>(KnownSettings.ReadabilityMax);

            string[] whitelist = settings.GetValue(KnownSettings.NiceList).Split(',');
            string[] blacklist = settings.GetValue(KnownSettings.NaughtyList).Split(',');

            List<string> longWords = new List<string>();
            List<string> blacklisted = new List<string>();

            text = Regex.Replace(text, KnownStrings.ClosingHtmlTags, ".");
            text = text.Replace(KnownStrings.NewLine, " ");
            text = Regex.Replace(text, KnownStrings.CharsToRemove, "").Replace("&amp;", "&");
            text = Regex.Replace(text, KnownStrings.DuplicateSpaces, " ");

            List<string> sentences = text.Split(KnownStrings.WordDelimiters).Where(s => s.HasValue() && s.Length > 3).ToList();

            // calc words/sentence
            double totalWords = 0;
            double totalSyllables = 0;

            foreach (string sentence in sentences)
            {
                List<string> words = sentence.Trim().Split(' ').Where(s => s.HasValue() && !whitelist.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
                totalWords += words.Count;

                foreach (string word in words)
                {
                    int syllableCount = CountSyllables(word);
                    if (syllableCount >= longWordLength && !longWords.Contains(word, StringComparer.OrdinalIgnoreCase))
                    {
                        longWords.Add(word);
                    }

                    if (blacklist.Contains(word, StringComparer.OrdinalIgnoreCase))
                    {
                        blacklisted.Add(word);
                    }

                    totalSyllables += syllableCount;
                }
            }

            double asl = totalWords / sentences.Count;
            double asw = totalSyllables / totalWords;

            double score = Math.Round(206.835 - (1.015 * asl) - (84.6 * asw), 0);

            return new ReadabilityResponseModel
            {
                Score = score,
                AverageSyllables = Math.Round(asw, 1),
                SentenceLength = Math.Round(asl, 1),
                LongWords = longWords.OrderBy(l => l).ToList(),
                Blacklist = blacklisted.OrderBy(l => l).ToList(),
                Failed = score < readabilityMin || score > readabilityMax || blacklisted.Any() || longWords.Any(),
                FailedReadability = score < readabilityMin || score > readabilityMax,
                TargetMin = readabilityMin,
                TargetMax = readabilityMax,
                LongWordSyllables = longWordLength
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        private static int CountSyllables(string word)
        {
            string currentWord = word;
            var numVowels = 0;
            var lastWasVowel = false;

            if (currentWord.Length <= 3)
            {
                return 1;
            }

            foreach (char character in currentWord.ToLower())
            {
                var foundVowel = false;

                foreach (char vowel in KnownStrings.Vowels)
                {
                    //don't count diphthongs
                    if (vowel == character && lastWasVowel)
                    {
                        foundVowel = true;
                        break;
                    }

                    if (vowel != character || lastWasVowel)
                    {
                        continue;
                    }

                    numVowels++;

                    foundVowel = true;
                    lastWasVowel = true;

                    break;
                }

                //if full cycle and no vowel found, set lastWasVowel to false;
                if (!foundVowel)
                {
                    lastWasVowel = false;
                }
            }

            string lastTwoChars = currentWord.Substring(currentWord.Length - 2).ToLower();
            string lastThreeChars = currentWord.Substring(currentWord.Length - 3).ToLower();

            if (KnownStrings.Endings.Contains(lastTwoChars) && lastThreeChars != "ied" || lastTwoChars.First() != 'l' && lastTwoChars.Last() == 'e')
            {
                numVowels--;
            }

            return numVowels;
        }
    }
}
