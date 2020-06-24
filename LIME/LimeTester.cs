using System;
using System.Text;
using System.Diagnostics;
using System.IO;
using static LIME.BuiltInMatcher;
using System.Linq;

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
            var Alphabets = Alphabet.Above1;

            TestBody("「Test07_0」", true, "ABC", Alphabet.Times(3));

            TestBody("「Test07」", true, "ABC", Begin + Alphabet.Times(3) + End);

            TestBody("「Test08」", true,
                "ABC", Begin + Alphabet + ("" + Alphabet).Times(2) + End);

            TestBody("「Test09」", true, "ABC", Begin + Alphabet.Times(3, ""._()) + End);

            TestBody("「繰り返し(デリミタ長さゼロ)」", false, "abc1", Begin + Alphabet.Times(3, ""._()) + End);

            var number = '0'.To('9');
            var numbers = number.Above1["整数"];

            TestBody("「Test10」", true, "0", numbers);

            TestBody("「Test11」", true, "01", numbers);


            TestBody("「Test12」", true, "012", numbers);

            Matcher matcherExe01 = numbers + "+";

            TestBody("「Test13」", true, "01+", numbers + "+");

            TestBody("「Test13_2」", true, "+01", "+" + numbers);

            TestBody("「Test14 ２項演算」", true, "0+2", numbers + "+" + numbers);

            TestBody("「Test15 ２項演算」", true, "0+2", numbers + "+" + number);

            TestBody("「Test15_2 ２項演算」", true, "0+2", number + "+" + numbers);

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
            DivExp.Inner =
                ((numbers | DivExp)["左辺"] +
                '/' +
                numbers["右辺"])["除算式"];

            // 「減算」のマッチャーを作る。(但し中身は空っぽ)
            RecursionMatcher SubExp = new RecursionMatcher();

            // 「減算」の中身を設定する。
            SubExp.Inner =
                ((numbers | DivExp | SubExp)["左辺"] +
                '-' +
                (numbers | DivExp)["右辺"])["減算式"];
            TestBody("「Test20 循環定義(左結合演算)」", true, "01-23/45-67-89", SubExp);

            // 「左シフト演算」のマッチャーを作る。(但し中身は空っぽ)
            RecursionMatcher LShiftExp = new RecursionMatcher();

            // 「左シフト演算」の中身を設定する。
            LShiftExp.Inner =
                ((numbers | DivExp | SubExp | LShiftExp)["左辺"] +
                "<<" +
                (numbers | DivExp | SubExp)["右辺"])["左シフト式"];
            TestBody("「Test21 循環定義(左結合演算)」", true, "012<<345/678-901-234", LShiftExp);


            var Cr = '\r'._();
            var Lf = '\n'._();

            // 文字列リテラル(C言語形式)
            var StringLiteral = '"'._() +
                (('\\' + (Cr | Lf).Not) | (Cr | Lf | '\\' | '"').Not).Above0["文字列中身"] +
                '"';
            Debug.WriteLine(StringLiteral.ToString());
            Debug.WriteLine(StringLiteral.ToTreeText());


            TestBody("「Test22 文字列リテラル」", true, "ab\"c\"89", StringLiteral);
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

            var testPattern = '"' + '"'._().Not.Above1["TextBody"] + '"';

            TestBody("「Test31 文字列リテラル」", true, "a\"01234567890\"", testPattern);

            //var c = numbers["数値"];
            //var intLiteral = '1'.To('9') + numbers;
            //var floatLiteral = 
            //        intLiteral + '.' + numbers + 
            //        ("e"._() | "E") + ("+"._() | "-")._01();


            //var LineStringLiteral = "%%"._() + (Cr | Lf).Not._0Max();


            //var cr = "\r";
            //var lf = "\n";
            //var crlf = cr + lf;
            ////var testText =
            ////    "block" + crlf +
            ////    "  012345" + crlf +
            ////    "  nnnnnn" + crlf +
            ////    "pppppp";
            //var testText =
            //    "block" + lf + cr +
            //    "  012345" + crlf +
            //    "    nnnnnn" + crlf +
            //    " block" + lf + cr +
            //    "  012345" + crlf +
            //    "   nnnnnn" + crlf +
            //    "pppppp";

            //var bnf = "stringliteral   ::=  [stringprefix](shortstring | longstring);";

            //TestBody("「Test31_1 BNF」", true, "s::=a;", GetBNF());
            //TestBody("「Test31_2 BNF」", true, "s ::= a ;", GetBNF());
            //TestBody("「Test31_3 BNF」", true, "s ::= [a] ;", GetBNF());
            //TestBody("「Test31_4 BNF」", true, "s ::= (a) ;", GetBNF());
            //TestBody("「Test31_5 BNF」", true, "s ::= b|c ;", GetBNF());
            //TestBody("「Test31_6 BNF」", true, "s ::= (b|c) ;", GetBNF());
            //TestBody("「Test31_7 BNF」", true, "s ::= [a](b|c) ;", GetBNF());

            //TokenStream tokenStream = new TokenStream(testText);

            //foreach (var token in tokenStream)
            //{
            //    Debug.WriteLine(token);
            //}

            //var newLine = Cr + Lf;
            //var spaces = " "._()._0Max();
            //var statement = spaces + LineChar._1Max() + newLine;

            //var blockRule =
            //    "block" + Cr + Lf +
            //    spaces + Indent + statement._1Max() +
            //    Dedent;



            //var parenExp = new RecursionMatcher();

            //var ID = (Alphabet | number)._1Max()["identifier"] |
            //    '<' + (Alphabet | number | ' ' | '-')._1Max()["identifier"] + '>';



            //parenExp.Inner =
            //    ('(' + parenExp["group_exp"] + ')')["group"] |
            //    ('[' + parenExp["option_exp"] + ']')["option"] |
            //    (parenExp["or_left"] + '|' + parenExp["or_right"])["or"] |
            //    (parenExp["connect_left"] + parenExp["connect_right"])["connect"] |
            //    (parenExp["above1_exp"] + '+')["above1"] |
            //    (parenExp["above0_exp"] + '*')["above0"] |

            //    ID |

            //    '#' + '#'._().Not._0Max()["literal"] + '#' | // Algol-60 形式
            //    '"' + '"'._().Not._0Max()["literal"] + '"' |
            //    '\'' + '\''._().Not._0Max()["literal"] + '\''
            //    ;

            //var Rule = (ID["rule_name"] + "::=" + parenExp["rule_exp"] + ';'._()._01())["rule"];


            //TestBody("「Test40 BNF」", true, "((a|b))", parenExp);
            //TestBody("「Test41 BNF」", true, "([a|b])", parenExp);
            //TestBody("「Test42 BNF」", true, "([a b])", parenExp);
            //TestBody("「Test43 BNF」", true, "([a* b])", parenExp);
            //TestBody("「Test44 BNF」", true, "([a+ b])", parenExp);
            //TestBody("「Test45 BNF」", true, "([a+ b])+", parenExp);
            //TestBody("「Test46 BNF」", true, "([<a b c>+ b])+", parenExp);
            //TestBody("「Test47 BNF」", true, "([aa+ b])+", parenExp);

            //TestBody("「Test48 BNF」", true, "s::=[a](b|c)", Alphabet + "::=" + parenExp);


            //TestBody("「Test49 BNF」", true, "s::=[a](b|c)", ID + "::=" + parenExp + ';'._()._01());
            //TestBody("「Test50 BNF」", true, "s::=[a](b|c)", Rule);
            //TestBody("「Test51 BNF」", true, "ss::=[aa](bb|cc)", Rule);

            //TestBody("「Test47 BNF」", true, "stringliteral::=[stringprefix](shortstring|longstring)", Rule);
        }


        /// <summary>
        /// BNF で記述された構文定義文字列からLIMEソースコードに変換する
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string BNFtoLIME(string text)
        {
            var BNF = GetBNF();
            var match = BNF.Search(text);
            var LIME = TagToString(text, match);
            return LIME;
        }

        /// <summary>
        /// 一般的に用いられているBNF用パーサを返信する
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// 
        /// ::= で代入する場合、 = がクォーテーションで囲われずそのまま書かれる場合がある
        /// <要素名> の場合、要素名に空白を含む場合がある
        /// 
        /// </remarks>
        public static Matcher GetBNF()
        {

            // <aaa> のように囲われた識別子
            var EnclosedID = '<' + '>'._().Not._1Max()["identifier"] + '>';
            var Cr = '\r'._();
            var Lf = '\n'._();

            var Blank = Cr | Lf | " " | "\t";

            // 囲われたリテラル
            var EnclosedLiteral =
                '#' + '#'._().Not._0Max()["literal"] + '#' | // Algol-60 形式
                '"' + '"'._().Not._0Max()["literal"] + '"' |
                '\'' + '\''._().Not._0Max()["literal"] + '\'';

            // 生リテラル
            var NakedLiteral = (' ' | Cr | Lf).Not._1Max(); ;

            // 囲われた識別子を使う式
            var EExp = new RecursionMatcher();

            var EOr = (EExp["or_left"] + '|' + EExp["or_right"])["or"];
            var EOption = ('[' + EExp["option_exp"] + ']')["option"];
            var EAbove0 = (EExp["above0_exp"] + '*')["above0"];
            var EAbove1 = (EExp["above1_exp"] + '+')["above1"];
            var EGroup = ('(' + EExp["group_exp"] + ')')["group"];
            var EConnect = EExp + EExp;

            EExp.Inner = EnclosedID | EnclosedLiteral | NakedLiteral | 
                EOr | EOption | EAbove0 | EAbove1 | EGroup | EConnect;
            var ERule = (EnclosedID["rule_name"] + "::=" + EExp["rule_exp"] + ';'._()._01())["rule"];

            // 囲われた識別子で定義されたBNF
            var EnclosedBNF = Begin + ERule._1Max() + End;


            var Alphabet = 'A'.To('Z') | 'a'.To('z');
            var Numeric = '0'.To('9');

            // 裸の識別子
            var NakedID = ((Alphabet | '_') + (Alphabet | '_' | Numeric)._0Max())["identifier"];

            // 裸の識別子を使う式
            var NExp = new RecursionMatcher();

            var NOption = ('[' + NExp["option_exp"] + ']')["option"];
            var NGroup = ('(' + NExp["group_exp"] + ')')["group"];

            var NOr = (NExp["or_left"] + '|' + NExp["or_right"])["or"];
            var NAbove0 = (NExp["above0_exp"] + '*')["above0"];
            var NAbove1 = (NExp["above1_exp"] + '+')["above1"];

            var NAbove = NAbove0 | NAbove1;
            var NotID = NOption | NGroup | EnclosedLiteral;

            var NConnect = NExp + NExp;
                //(NOr| NakedID) + NotID |
                //NAbove + NotID |
                //NOr + NotID |
                //NOr + NotID |
                //NOr + NotID |
                //NOr + NotID |
                //(NAbove | NotID) + (NOr | NAbove | NotID | NakedID);

            NExp.Inner = NakedID | EnclosedLiteral |
                NOr | NOption | NAbove | NGroup | NConnect;

            var NRule = (NakedID["rule_name"] + "::=" + NExp["rule_exp"] + ';'._()._01())["rule"];
            // 裸の識別子で定義されたBNF
            var NakedBNF = Begin + NRule._1Max() + End;


            return NakedBNF | EnclosedBNF;
        }




        public static string TagToString(string text, Match match)
        {
            StringBuilder sb = new StringBuilder();

            //foreach (var tagMatch in match.TagChildren)
            //{
            //    string value;

            //    switch (tagMatch.Tag)
            //    {
            //    case "rule":
            //        TagMatch[] items = tagMatch.TagChildren.ToArray();
            //        var rule_name = TagToString(text, items[0]);
            //        var rule_exp = TagToString(text, items[1]);
            //        sb.Append($"var {rule_name} = {rule_exp};\r\n");
            //        break;
            //    case "identifier":
            //        value = tagMatch.ToString(text);
            //        value = value.Replace(' ', '_').Replace('-', '_');
            //        sb.Append(value);
            //        break;
            //    case "or":
            //        TagMatch[] or_items = tagMatch.TagChildren.ToArray();
            //        var or_left = TagToString(text, or_items[0]);
            //        var or_right = TagToString(text, or_items[1]);
            //        sb.Append(or_left + "|" + or_right);
            //        break;
            //    case "option":
            //        var option_exp = tagMatch.TagChildren.First();
            //        sb.Append(TagToString(text, option_exp) + "._01()");
            //        break;
            //    case "above0":
            //        var above0_exp = tagMatch.TagChildren.First();
            //        sb.Append(TagToString(text, above0_exp) + "._0Max()");
            //        break;
            //    case "above1":
            //        var above1_exp = tagMatch.TagChildren.First();
            //        sb.Append(TagToString(text, above1_exp) + "._1Max()");
            //        break;
            //    case "group":
            //        var group_exp = tagMatch.TagChildren.First();
            //        sb.Append("(" + TagToString(text, group_exp) + ")");
            //        break;
            //    case "literal":
            //        sb.Append(tagMatch.ToString(text));
            //        break;
            //    }
            //}

            return sb.ToString();
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
                var match = Match.SelectBestMatch(exe.FinishedMatches);//.ToOutputMatch();

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

                var tagedTree = match.ToTagedTree();

                WriteMatch(text, tagedTree);

                //var filteredMatch = match.ToOutputMatch_ByTag();
                //if(filteredMatch != null)
                //{
                //    WriteMatch(text, filteredMatch);
                //}
                //else
                //{
                //    WriteMatch(text, match);
                //}
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

        private static void WriteMatch(string text, MinimalMatch match)
        {
            WriteMatch(text, match, "");
        }
        private static void WriteMatch(string text, MinimalMatch match, string indent)
        {
            Debug.WriteLine(
                    $"[{match.Begin}-{match.End}]{indent}{match.ToString(text)} [{match.Tag}]"
                       );
            foreach(var subMatch in match)
            {
                WriteMatch(text, subMatch, indent + "  ");
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
            //if (match is CaptureMatch captureMatch)
            //{
            //    var tag = captureMatch.Tag;
            //    int count = 0;
            //    sbWork.Append("[");
            //    sbWork.Append(tag);
            //    sbWork.Append("]");
            //}

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
