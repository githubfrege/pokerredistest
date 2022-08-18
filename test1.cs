using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Numerics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using StackExchange.Redis;


namespace PokerBotApplication
{
    public partial class ManualPokerBot : Form
    {
        public ManualPokerBot()
        {
            HandEvaluation.GenerateAllKeyValuePairs();
            PreFlopOperations.GenerateValueMatrix(PreFlopOperations.PreflopMatrixSuited, @"suited.csv");
            PreFlopOperations.GenerateValueMatrix(PreFlopOperations.PreflopMatrixUnsuited, @"unsuited.csv");
            InitializeComponent();
        }


        private void resultBtn_Click(object sender, EventArgs e)
        {

            List<KeyValuePair<int, int>> holeCards = Parser.ToKvpList(holeCardsTxt.Text);
            List<KeyValuePair<int, int>> communityCards = Parser.ToKvpList(communityCardsTxt.Text);
            if (holeCards.Count == 2)
            {
                try

                {
                    if ((communityCards.Count < 3 || communityCards.Count > 5) && communityCards.Count != 0)
                    {
                        MessageBox.Show("Add your community cards!");
                    }
                    else
                    {
                        float odds = communityCards.Count == 0 ? (float)PreFlopOperations.GetPreflopOdds(holeCards) : BotOperations.GetOdds(holeCards, communityCards);
                        int bankRoll = Regex.IsMatch(bankRollTxt.Text, @"^\d+$") && (int.Parse(bankRollTxt.Text) >= int.Parse(myChipsTxt.Text)) ? int.Parse(bankRollTxt.Text) : int.Parse(myChipsTxt.Text);
                        int betAmount = BotOperations.WhatToBet(odds, int.Parse(tableChipsTxt.Text), int.Parse(opponentBetTxt.Text), int.Parse(myChipsTxt.Text),bankRoll, out float expectedValue);
                        if (!(expectedValue > 0)) {
                            MessageBox.Show("fold!");
                        }
                        else if (betAmount == int.Parse(opponentBetTxt.Text))
                        {
                            if (int.Parse(opponentBetTxt.Text) == 0)
                            {
                                MessageBox.Show($"You should check for an Expected Value of +{expectedValue}$");
                            }
                            else
                            {
                                MessageBox.Show($"You should call for an Expected Value of +{expectedValue}$");
                            }
                          
                        }
                        else
                        {
                            if (int.Parse(opponentBetTxt.Text) == 0)
                            {
                                MessageBox.Show($"You should bet {betAmount} for an Expected Value of +{expectedValue}$");
                            }
                            else
                            {
                                MessageBox.Show($"You should raise to {betAmount} for an Expected Value of +{expectedValue}$");
                            }
                        }

                    }
                    
                  
                }
                catch
                {
                    MessageBox.Show("Input incorrect, did you forget to put in chip values?");
                }
               
            }
            else
            {
                MessageBox.Show("You need two hole cards!");
            }
        }
    }
    public static class Parser
    {
        private static Dictionary<char, int> _rankNumberMapping = new Dictionary<char, int> { ['T'] = 10, ['J'] = 11, ['Q'] = 12, ['K'] = 13, ['A'] = 14 };
        private static Dictionary<int, char> _numberRankMapping = new Dictionary<int, char> { [10] = 'T', [11] = 'J', [12] = 'Q', [13] = 'K', [14] = 'A' };
        private static Dictionary<int, char> _numberSuitMapping = new Dictionary<int, char> { [0] = 'c', [1] = 'd', [2] = 'h', [3] = 's' };
        private static Dictionary<char, int> _suitNumberMapping = new Dictionary<char, int> { ['c'] = 0, ['d'] = 1, ['h'] = 2, ['s'] = 3 };


        public static List<KeyValuePair<int, int>> ToKvpList(this string str)
        {
            var result = new List<KeyValuePair<int, int>>();
            string pattern = @"^((^|\s)[23456789TJQKA][chds])+$";
            Match m = Regex.Match(str.TrimEnd(), pattern, RegexOptions.IgnoreCase);
            if (m.Success)
            {
                foreach (string card in str.Split(' '))
                {
                    int rank = Char.IsLetter(card[0]) ? _rankNumberMapping[card[0]] : int.Parse(card[0].ToString());
                    result.Add(new KeyValuePair<int, int>(rank, _suitNumberMapping[card[1]]));
                }
            }
            return result;
        }
        public static string ToCardString(this List<KeyValuePair<int, int>> kvpList)
        {
            string str = "";
            foreach (var kvp in kvpList)
            {
                char rank = kvp.Key > 9 ? _numberRankMapping[kvp.Key] : Char.Parse(kvp.Key.ToString());
                str += rank + _numberSuitMapping[kvp.Value] + " ";
            }
            return str;
        }
    }
    public static class PreFlopOperations
    {
        public static double[,] PreflopMatrixSuited = new double[14, 14];
        public static double[,] PreflopMatrixUnsuited = new double[14, 14];

        public static void GenerateValueMatrix(double[,] matrix, string path)
        {
            using (TextFieldParser parser = new TextFieldParser(path))
            {
                bool firstLine = true;
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                int i = 0;
                int j = 0;
                while (!parser.EndOfData)
                {

                    //Processing row
                    string[] fields = parser.ReadFields();
                    if (firstLine)
                    {
                        firstLine = false;

                        continue;
                    }
                newLine:
                    bool firstField = true;
                    foreach (string field in fields)
                    {
                        if (firstField)
                        {
                            firstField = false;

                            continue;
                        }
                        if (!String.IsNullOrEmpty(field))
                        {
                            string newField = field.Replace(',', '.').Trim(new char[] { '%', '"' });
                            double percentNumber = double.Parse(newField, CultureInfo.InvariantCulture);
                            double frac = percentNumber / 100;
                            matrix[13 - i,13 - j] = frac;
                        }

                        i++;
                        if (i > 13)
                        {
                            i = 0;
                            j++;
                            goto newLine;
                        }
                    }
                }
            }
        }
        public static double GetPreflopOdds(List<KeyValuePair<int, int>> holeCards)
        {

            return holeCards[0].Value.Equals(holeCards[1].Value) ? PreflopMatrixSuited[holeCards[0].Key - 1, holeCards[1].Key - 1] : PreflopMatrixUnsuited[holeCards[0].Key - 1, holeCards[1].Key - 1];

        }
    }
    public static class BotOperations
    {

        private static float getRiskOfRuin(float odds, int i, int bankRoll)
        {
            float riskOfRuin(int units)
            {
                return (float)Math.Pow((1 - odds) / odds, units);
            }
            return riskOfRuin(bankRoll / (i + 1));


        }

        public static int WhatToBet(float odds, int tableChips, int chipOffer, int myChips, int bankRoll, out float expectedValue)
        {
            
            int bet = 0;
            float maxEv = 0;
            for (int i = chipOffer; i <= myChips + chipOffer; i++)
            {
                
                float currentEV = (odds * (tableChips + i)) + ((1 - odds) * -(i));
                if (currentEV > maxEv && getRiskOfRuin(odds,i,bankRoll) < 0.11f)
                {
                    maxEv = currentEV;
                    bet = i;
                }
            }
            expectedValue = maxEv;
            return bet;
        }

        private static (float heroPoints, float villainPoints) updateScore(ulong holes, ulong villainHoles, ulong currentHand)
        {
            (int mainScore, long tieBreaker) heroHand = HandEvaluation.HandToPlay(holes, currentHand);
            (int mainScore, long tieBreaker) villainHand = HandEvaluation.HandToPlay(villainHoles, currentHand);
            switch (heroHand.mainScore.CompareTo(villainHand.mainScore) != 0 ? heroHand.mainScore.CompareTo(villainHand.mainScore) : heroHand.tieBreaker.CompareTo(villainHand.tieBreaker))
            {
                case 1:
                    return (1f, 0f);
                case -1:
                    return (0f, 1f);
                default:
                    return (0f, 0f);
            }

        }
        public static float GetOdds(List<KeyValuePair<int, int>> holeCardsKvpList, List<KeyValuePair<int, int>> tableKvpList)
        {
            float heroWins = 0; //if player wins
            float villainWins = 0; //if opponent wins
            ulong holes = HandEvaluation.ParseAsBitField(holeCardsKvpList);
            ulong communityCards = HandEvaluation.ParseAsBitField(tableKvpList);
            ulong availableCards = HandEvaluation.Deck ^ (holes | communityCards);
            foreach (ulong villainHoles in HandEvaluation.CardCombos(availableCards.ToIEnum(), 2)) //every combination of cards our opponent may have
            {
                ulong currentAvailableCards = availableCards ^ villainHoles;
                if (tableKvpList.Count < 5)
                {
                    foreach (ulong cardAdditions in HandEvaluation.CardCombos(currentAvailableCards.ToIEnum(), 5 - tableKvpList.Count)) //all combinations of cards that may be added to the existing table
                    {
                        ulong currentHand = communityCards | cardAdditions;
                        (float heroPoints, float villainPoints) scoreUpdate = updateScore(holes, villainHoles, currentHand);
                        heroWins += scoreUpdate.heroPoints;
                        villainWins += scoreUpdate.villainPoints;

                    }
                }
                else
                {

                    (float heroPoints, float villainPoints) scoreUpdate = updateScore(holes, villainHoles, communityCards);
                    heroWins += scoreUpdate.heroPoints;
                    villainWins += scoreUpdate.villainPoints;
                }


            }
            return heroWins / (heroWins + villainWins);

        }
    }
    public static class HandEvaluation
    {
        public static ulong Deck = 0b_11111111_1111100_11111111_1111100_11111111_1111100_11111111_1111100;
        public static Dictionary<ulong, (int, long)> _scoreTable = new Dictionary<ulong, (int, long)>();
        public static Dictionary<(ulong, ulong), (int, long)> _handTable = new Dictionary<(ulong, ulong), (int, long)>();
        public static Dictionary<ulong, long> _tieBreakerTable = new Dictionary<ulong, long>();
        public static Dictionary<(ulong, bool), int> _mainScoreTable = new Dictionary<(ulong, bool), int>();
        public static ConnectionMultiplexer Redis = ConnectionMultiplexer.Connect("localhost");
        public static IDatabase Db = Redis.GetDatabase();
        public static void GenerateAllKeyValuePairs()
        {
            var keys = Redis.GetServer("localhost", 6379).Keys();
            foreach (var key in keys)
            {
                if (key.ToString().Contains("fullscore"))
                {
                    string[] strArray = Db.StringGet(key).ToString().Split(" ");
                    _scoreTable[ulong.Parse(key.ToString().Remove(9))] = (int.Parse(strArray[0]), long.Parse(strArray[1]));
                }
                else if (key.ToString().Contains("mainscore"))
                {
                    string[] strArray = key.ToString().Remove(9).Split(" ");
                    _mainScoreTable[(ulong.Parse(strArray[0]), bool.Parse(strArray[1]))] = int.Parse(Db.StringGet(key));
                }
                else if (key.ToString().Contains("hand"))
                {
                    string[] inputArray = key.ToString().Remove(5).Split(" ");
                    string[] outputArray = Db.StringGet(key).ToString().Split(" ");
                    _handTable[(ulong.Parse(inputArray[0]), ulong.Parse(inputArray[1]))] = (int.Parse(outputArray[0]), long.Parse(outputArray[1]));
                }
            }

        }
        public static ulong ParseAsBitField(List<KeyValuePair<int, int>> cards)
        {
            ulong bf = 0;
            foreach (var card in cards)
            {
                bf |= 1UL << (card.Key + (15 * card.Value));
            }
            return bf;

        }
        private static bool isStraight(int solo)
        {
            int lsb = solo & -solo;

            int normalized = solo / lsb;

            return normalized == 31 || solo == 16444;

        }
        public static int getMainScore(int solo, ulong ranksField, bool flush)
        {
            if (_mainScoreTable.TryGetValue((ranksField, flush), out int value))
            {
                return value;
            }
            bool straight = isStraight(solo);
            if (straight && flush)
            {
                if (solo == 31744)
                {

                    return 10;
                }
                else
                {
                    return 9;
                }

            }
            else
            {
                switch (ranksField % 15)
                {
                    case 1:
                        return 8;
                    case 10:
                        return 7;

                    case 9:
                        return 4;
                    case 7:
                        return 3;
                    case 6:
                        return 2;
                    default:
                        break;
                }

                if (flush)
                {
                    return 6;
                }
                else if (straight)
                {
                    return 5;
                }
                return 1;

            }
        }
        private static int getHighestRank(ulong ranksField, ref int pos)
        {
            pos = 63 - BitOperations.LeadingZeroCount((ulong)ranksField | 1);

            return (int)Math.Floor((double)(pos / 4));
        }
        private static long getTieBreaker(ulong ranksField)
        {
            if (_tieBreakerTable.TryGetValue(ranksField, out long value))
            {
                return value;
            }

            int pos = 0;
            int tiebreaker = 0;
            for (int i = 0; i < 5; i++)
            {
                int highestRank = getHighestRank(ranksField, ref pos);
                ranksField ^= (1UL << pos);
                tiebreaker |= (highestRank << (16 - (4 * i)));
            }

            Db.StringSet("tiebreaker"+ranksField.ToString(), tiebreaker);
            _tieBreakerTable[ranksField] = tiebreaker;
            return tiebreaker;
        }
        private static void getFields(ulong bf, out int solo, out ulong ranksField, out bool flush)
        {
            solo = 0;
            ranksField = 0;
            flush = false;
            Dictionary<int, int> instances = new Dictionary<int, int>();
            int cards = 0;
            for (int i = 0; i < 4; i++)
            {
                int flushIdx = 0;
                for (int j = 2; j <= 14; j++)
                {


                    if ((bf & (1UL << (j + (15 * i)))) > 0)
                    {
                        cards++;
                        solo |= (1 << j);
                        flushIdx++;
                        if (flushIdx == 5)
                        {
                            flush = true;
                        }
                        if (!instances.ContainsKey(j))
                        {
                            instances.Add(j, 0);
                        }
                        else
                        {
                            instances[j] = instances[j] + 1;
                        }

                        int offset = instances[j];
                        ulong addition = 1UL << (j << 2);
                        addition = addition << offset;
                        ranksField |= addition;

                    }

                }
            }

        }
        public static IEnumerable<ulong> ToIEnum(this ulong num)
        {
            for (int i = 2; i <= 60; i++)
            {
                if ((num & (1UL << i)) > 0)
                {
                    yield return 1UL << i;
                }
            }
        }
        public static IEnumerable<ulong> CardCombos(IEnumerable<ulong> cards, int count)
        {
            int i = 0;
            foreach (var card in cards)
            {
                if (count == 1)
                {
                    yield return card;
                }

                else
                {
                    foreach (var result in CardCombos(cards.Skip(i + 1), count - 1))
                    {

                        yield return result | card;
                    }
                }

                ++i;
            }
        }

        public static (int mainScore, long tieBreaker) GetFullScore(ulong bf)
        {
            if (_scoreTable.TryGetValue(bf, out (int, long) value))
            {
                return value;
            }
            getFields(bf, out int solo, out ulong ranksField, out bool flush);
            int mainScore = getMainScore(solo, ranksField, flush);
            (int mainScore, long tieBreaker) result = (mainScore, getTieBreaker(ranksField));
            _mainScoreTable[(ranksField, flush)] = mainScore;
            _scoreTable[bf] = result;
            Db.StringSet("fullscore"+bf, result.mainScore + " " + result.tieBreaker);
            Db.StringSet("mainscore"+ranksField+" "+flush,mainScore);
            return result;
        }
        public static (int mainScore, long tieBreaker) HandToPlay(ulong holes, ulong cardsOnTable)
        {
            if (_handTable.TryGetValue((holes, cardsOnTable), out (int, long) value))
            {
                return value;
            }

            (int mainScore, long tieBreaker) max = (-100000, -100000);
            foreach (ulong combo in CardCombos(cardsOnTable.ToIEnum(), 3))
            {
                (int mainScore, long tieBreaker) currentScore = GetFullScore(combo | holes);
                if ((currentScore.mainScore.CompareTo(max.mainScore) != 0 ? currentScore.mainScore.CompareTo(max.mainScore) : currentScore.tieBreaker.CompareTo(max.tieBreaker)) > 0)
                {
                    max = currentScore;
                }

            }
            foreach (ulong combo in CardCombos(cardsOnTable.ToIEnum(), 4))
            {
                foreach (ulong holeCard in holes.ToIEnum())
                {
                    (int mainScore, long tieBreaker) currentScore = GetFullScore(combo | holeCard);
                    if ((currentScore.mainScore.CompareTo(max.mainScore) != 0 ? currentScore.mainScore.CompareTo(max.mainScore) : currentScore.tieBreaker.CompareTo(max.tieBreaker)) > 0)
                    {
                        max = currentScore;
                    }

                }
            }
            _handTable[(holes, cardsOnTable)] = max;
            Db.StringSet("hand"+holes + " " + cardsOnTable, max.mainScore + " " + max.tieBreaker);
            return max;
        }
    }
    
}
