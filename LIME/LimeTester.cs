using System;
using System.Text;
using System.Diagnostics;
using System.IO;
using static LIME.BuiltInMatcher;

namespace LIME
{
    #region テスト用
    public abstract class TestClass
    {
        /// <summary>
        /// テスト用関数
        /// </summary>
        public static void Test()
        {
            


            TestBody("「Test01」", true, "A", Begin + 'A' + End);

            TestBody("「Test02」", true, "ABC", 'A'._() + 'B' + 'C');

            TestBody("「Test03」", true, "ABC", ('A'._() | 'B') + 'B' + 'C');

            TestBody("「Test04」", true, "ABC", 'A'._().Times(1) + 'B' + 'C');

            TestBody("「Test05」", true, "ABC", ('A'._() | 'B').Times(1) + 'B' + 'C');

            TestBody("「Test06」", true, "ABC", ('A'._() | 'B').Times(2) + 'C');

            TestBody("「Test01_2」", true, "AA", 'A'._().Times(2));

            TestBody("「Test01_3」", true, "AAA", 'A'._().Times(3));

            TestBody("「Test01_4」", true, "AAAA", 'A'._().Times(4));

            TestBody("「Test06_2」", true, "ABC", ('A'._() | 'B' | 'C').Times(3));

            var Alphabet = 'a'.To('z') | 'A'.To('Z');

            TestBody("「Test07_0」", true, "ABC", Alphabet.Times(3));

            TestBody("「Test07」", true, "ABC", Begin + Alphabet.Times(3) + End);

            TestBody("「Test08」", true, "ABC", Begin + Alphabet + ("" + Alphabet).Times(2) + End);

            TestBody("「Test09」", true, "ABC", Begin + Alphabet.Times(3, ""._()) + End);

            TestBody("「繰り返し(デリミタ長さゼロ)」", false, "abc1", Begin + Alphabet.Times(3, ""._()) + End);

            var number = '0'.To('9');
            var numbers = number._1Max();

            TestBody("「Test10」", true, "012", numbers * 3);

            TestBody("「Test11」", true, "012", numbers.Times(2, 3));


            TestBody("「Test12」", true, "012", numbers);

            Matcher matcherExe01 = numbers + "+";

            TestBody("「Test13」", true, "01+", matcherExe01);


            TestBody("「Test14 ２項演算」", true, "0+2", numbers + "+" + numbers);

            TestBody("「Test15 ２項演算」", true, "0 + 2", numbers + "+" + numbers);

            TestBody("「Test16 ２項演算」", true, "01+23", numbers + '+' + numbers);

            TestBody("「Test16_2 ２項演算」", true, "01 + 23", numbers + '+' + numbers);

            TestBody("「Test17 ２項演算の連続」", true, "01+23+45", numbers + '+' + numbers + '+' + numbers);

            TestBody("「Test17_2 ２項演算の連続」", true, "01 + 23 + 45", numbers + '+' + numbers + '+' + numbers);

            RecursionMatcher exp = new RecursionMatcher();

            exp.Inner =
                ((exp | numbers) + '+'._()["演算子"] + numbers["整数"])["式"];
            TestBody("「Test18 循環定義(左結合演算)」", true, "01 + 23 + 45     + 67", exp);

            exp.Inner =
                ((exp | numbers) + '+'._() + numbers);
            TestBody("「Test18_2 循環定義(左結合演算)」", true, "01+23+45+67", exp);

            exp.Inner =
                (numbers["整数"] + '='._()["演算子"] + (exp | numbers["整数"]))["式"];
            TestBody("「Test19 循環定義(右結合演算)」", true, "01=23=45=67", exp);

            exp.Inner =
                (numbers + '=' + (exp | numbers));
            TestBody("「Test19_2 循環定義(右結合演算)」", true, "01=23=45=67", exp);

            // 「除算」のマッチャーを作る。(但し中身は空っぽ)
            RecursionMatcher DivExp = new RecursionMatcher();

            // 「除算」の中身を設定する。
            DivExp.Inner = ((numbers["整数"] | DivExp) + '/' + numbers["整数"])["除算式"];

            // 「減算」のマッチャーを作る。(但し中身は空っぽ)
            RecursionMatcher SubExp = new RecursionMatcher();

            // 「減算」の中身を設定する。
            SubExp.Inner = ((numbers["整数"] | DivExp | SubExp) + '-' + (numbers["整数"] | DivExp))["減算式"];
            TestBody("「Test20 循環定義(左結合演算)」", true, "01-23/45-67-89", SubExp);

            // 「左シフト演算」のマッチャーを作る。(但し中身は空っぽ)
            RecursionMatcher LShiftExp = new RecursionMatcher();

            // 「左シフト演算」の中身を設定する。
            LShiftExp.Inner = ((numbers["整数"] | DivExp | SubExp | LShiftExp) + "<<" + (numbers["整数"] | DivExp | SubExp))["左シフト式"];
            TestBody("「Test21 循環定義(左結合演算)」", true, "012<<345/678-901-234", LShiftExp);

            var Cr = '\r'._();
            var Lf = '\n'._();

            // 文字列リテラル(C言語形式)
            var StringLiteral = '"'._() + 
                (('\\' + (Cr | Lf ).Not) | (Cr | Lf | '\\' | '"').Not)._0Max() + 
                '"';

            TestBody("「Test22 文字列リテラル」", true, "dd\"a\"89", StringLiteral);
            TestBody("「Test23 文字列リテラル」", true, "z\"a\"ee", StringLiteral);
            TestBody("「Test24 文字列リテラル」", true, "z\"ab\"ee", StringLiteral);
            TestBody("「Test25 文字列リテラル」", true, "z\"01-23\"ee", StringLiteral);
            TestBody("「Test26 文字列リテラル」", true, "z\"01-23/45\"ee", StringLiteral);
            TestBody("「Test27 文字列リテラル」", true, "z\"01-23/4578\"ee", StringLiteral);
            TestBody("「Test28 文字列リテラル」", true, "z\"\\\"d\"ee", StringLiteral);
            TestBody("「Test29 文字列リテラル」", true, "z\"a\"ee", StringLiteral);
            TestBody("「Test30 文字列リテラル」", true, "z\"\"ee", StringLiteral);
            TestBody("「Test30_2 文字列リテラル」", true, "z\"  \"ee", StringLiteral);
            TestBody("「Test30_3 文字列リテラル」", true, "z  \"  \"  ee", StringLiteral);

            var LineStringLiteral = "%%"._() + (Cr | Lf).Not._0Max();


            var cr = "\r";
            var lf = "\n";
            var crlf = cr + lf;
            //var testText =
            //    "block" + crlf +
            //    "  012345" + crlf +
            //    "  nnnnnn" + crlf +
            //    "pppppp";
            var testText =
                "block" + lf + cr +
                "  012345" + crlf +
                "    nnnnnn" + crlf +
                " block" + lf + cr +
                "  012345" + crlf +
                "   nnnnnn" + crlf +
                "pppppp";



            TokenStream tokenStream = new TokenStream(testText);

            foreach (var token in tokenStream)
            {
                Debug.WriteLine(token);
            }

            //var newLine = Cr + Lf;
            //var spaces = " "._()._0Max();
            //var statement = spaces + LineChar._1Max() + newLine;

            //var blockRule =
            //    "block" + Cr + Lf +
            //    spaces + Indent + statement._1Max() +
            //    Dedent;


        }

        private static bool OmitMode = false;

        /// <summary>
        /// テスト用関数の本体
        /// </summary>
        /// <param name="testTitle">テストタイトル</param>
        /// <param name="requireResult">要求される結果。成功／失敗</param>
        /// <param name="matcher">マッチャー</param>
        /// <param name="text">入力文字列</param>
        private static void TestBody
            (string testTitle, bool requireResult, string text, Matcher matcher)
        {

            // 実行者を作成する。
            Executor exe = new Executor(matcher, text);

            bool matchExists = false;

            // １文字ずつ入力を消費して返信を得る。
            foreach (var exeResult in exe)
            {
                matchExists = true;
            }
            
            if (matchExists)
            {
                // 最も早く出現し、かつその内で最も長いマッチを返す。
                var match = Match.SelectBestMatch(exe.FinishedMatches);

                var sbOutPut = new StringBuilder();

                sbOutPut.AppendLine("....|....|....|....|");

                // テストタイトルを出力する。
                sbOutPut.AppendLine(testTitle);

                if (requireResult == true)
                {
                    sbOutPut.AppendLine("マッチ有り(予定通り)");

                    if(OmitMode)
                    {
                        if (text == match.ToString(text))
                        {
                            Debug.WriteLine(sbOutPut.ToString());
                            return;
                        }

                    }
                }
                else
                {
                    sbOutPut.AppendLine("マッチ有り(想定外)");
                }

                sbOutPut.Append("入力 = ");
                sbOutPut.Append(text);
                sbOutPut.AppendLine();

                sbOutPut.Append("一致 = ");
                sbOutPut.Append(match.ToString(text));
                sbOutPut.AppendLine();

                sbOutPut.Append("index = ");
                sbOutPut.Append(match.TextBegin);
                sbOutPut.AppendLine();

                sbOutPut.Append("End = ");
                sbOutPut.Append(match.TextEnd);
                sbOutPut.AppendLine();

                sbOutPut.Append("Match数 = ");
                sbOutPut.Append(exe.FinishedMatches.Count);
                sbOutPut.AppendLine();

                //// 帰ってきたマッチ全てを出力する。
                //foreach (var match in exe.FinishedMatches)
                //{
                //    WriteMatch(sbOutPut, sbWork, match, "");
                //}

                Debug.WriteLine(sbOutPut.ToString());
                
                var filteredMatch = match.ToOutputMatch_ByTag();
                if(filteredMatch != null)
                {
                    WriteMatch(text, filteredMatch);
                }
                else
                {
                    WriteMatch(text, match);
                }
            }
            else
            {
                var sbOutPut = new StringBuilder();
                var sbWork = new StringBuilder();

                sbOutPut.AppendLine("....|....|....|....|");

                // テストタイトルを出力する。
                sbOutPut.AppendLine(testTitle);

                if (requireResult == true)
                {
                    sbOutPut.AppendLine("マッチ無し(想定外)");
                }
                else
                {
                    sbOutPut.AppendLine("マッチ無し(予定通り)");
                }

                Debug.WriteLine(sbOutPut.ToString());
            }
        }

        /// <summary>
        /// マッチを子要素まで再起して出力する。
        /// </summary>
        /// <param name="match"></param>
        private static void WriteMatch(string text, Match match)
        {
            if(match == null)
            {
                Debug.WriteLine("match は null でした。");
                return;
            }

            var sbOutPut = new StringBuilder();
            var sbWork = new StringBuilder();
            WriteMatch_Body(text, sbOutPut, sbWork, match, "");
            Debug.WriteLine(sbOutPut.ToString());
        }

        /// <summary>
        /// マッチを子要素まで再帰して出力する処理の本体部
        /// </summary>
        /// <param name="sbOutPut">出力対象StringBuilder</param>
        /// <param name="sbWork">作業用StringBuilder</param>
        /// <param name="match">マッチインスタンス</param>
        /// <param name="indent">インデント</param>
        private static void WriteMatch_Body
            (string text, StringBuilder sbOutPut, StringBuilder sbWork, Match match, string indent)
        {
            // 一致文字列として表示する文字数
            const int MaxLength = 30;

            sbWork.Clear();

            // 文字範囲を出力する。
            sbWork.Append(indent);
            sbWork.Append("[");
            sbWork.Append(match.TextBegin.ToString());
            sbWork.Append("-");
            sbWork.Append(match.TextEnd.ToString());
            sbWork.Append("] ");
            string debugName = null;
#if DEBUG
            debugName = match.Generator.DebugName;
#endif
            if (debugName != null)
            {
                sbWork.Append("(" + debugName + ")");
            }
            if (match is TagMatch tagMatch)
            {
                var tags = tagMatch.Tags;
                int count = 0;
                sbWork.Append("[");
                foreach (var tag in tags)
                {
                    count++;
                    if (count > 1)
                    {
                        sbWork.Append(",");
                    }
                    sbWork.Append(tag);
                }
                sbWork.Append("]");
            }

            sbWork.Append(" ");
            sbWork.Append(match.UniqID);

            sbOutPut.AppendLine(sbWork.ToString());

            sbWork.Clear();
            sbWork.Append(indent);
            string matchValue = match.ToString(text);

            // 一致文字列が長すぎてそのまま出力できない時
            if (matchValue.Length >= MaxLength)
            {
                // 切り詰める。
                sbWork.Append(matchValue.Substring(0, MaxLength));
                sbWork.Append("...");
            }
            else
            {
                sbWork.Append(matchValue);
            }
            sbWork.Append(" (");
            sbWork.Append(match.DebugName);
            sbWork.Append(") " + match.Generator.UniqID);

            sbOutPut.AppendLine(sbWork.ToString());

            // 再帰でサブマッチを処理する。
            foreach (var subMatch in match.SubMatches)
            {
                WriteMatch_Body(text, sbOutPut, sbWork, subMatch, indent + "  ");
            }
        }


        public static void CloneTest(Matcher matcher)
        {
            string sorceText = matcher.ToString();
            Debug.WriteLine("Sorce = " + sorceText);

            string cloneText = matcher.ToString();
            Debug.WriteLine("Clone = " + cloneText);

            string rangeText04 = (matcher * new Range(0, 4)).ToString();
            Debug.WriteLine("Range(0,4) = " + rangeText04);

            string timesText0 = matcher.Times(0).ToString();
            Debug.WriteLine("Times(0) = " + timesText0);

            string timesText1 = matcher.Times(1).ToString();
            Debug.WriteLine("Times(1) = " + timesText1);

            string timesText2 = matcher.Times(2).ToString();
            Debug.WriteLine("Times(2) = " + timesText2);

            string timesText2Star = matcher.Times(2, "★"._()).ToString();
            Debug.WriteLine("Times(2,★) = " + timesText2Star);



        }

        public static void TestFunc(string text, Matcher matcher)
        {
            _text = text;

            Debug.WriteLine("---------------------");

            // マッチを文字列化して出力しておく。
            Debug.WriteLine("Matcher : " + matcher.ToString());

            Debug.WriteLine("text    : " + text);

            Match match = null;// MatchExecutor.Execute(text, matcher);


            if (match != null)
            {
                foreach (var item in match)
                {
                    Debug.WriteLine("Result  : " + item.ToString());
                }
                if (match.TextLength == 0)
                {
                    Debug.WriteLine("マッチ長さゼロです。");
                }
            }
            else
            {
                Debug.WriteLine("Matchがnullでした。");
            }
        }

        /// <summary>
        /// ファイルからテストデータを読み出して内容をstring型として返信する。
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static String LoadTestData(string path = "text.txt")
        {
            string text = "";
            if (File.Exists(path) == false)
            {
                Debug.WriteLine("テストデータのファイルが有りません！");
                return text;
            }

            using (var streamReader = new StreamReader(path, Encoding.UTF8))
            {
                text = streamReader.ReadToEnd();
            }

            return text;
        }

        /// <summary>
        /// テストデータをファイルに保存する。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="path"></param>
        public static void SaveTestData(string data, string path = "text.txt")
        {
            using (var writeer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writeer.Write(data);
            }
        }


        private static string _text;
        public static string Text
        {
            get
            {
                return _text;
            }
        }
    }
    #endregion テスト用

}
