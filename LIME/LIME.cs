using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;


/// <summary>
/// Language-Integrated-Matching-Evaluator (言語 統合 一致 評価器)
/// 
/// 概説
/// 　演算子オーバーロードにより、
/// 　正規表現やＢＮＦに近い表記でパターンマッチングを表現可能。
/// 　正規表現と異なり、無限段のネストを表現できる。
/// 　ボトムアップ方式で実行するので左再帰性の問題が起きない。
/// 
/// 　Matcherが単位となり、Matcherの木構造で複雑な文法を表現する。
/// 　Matcher同士を組み合わせてより大きなMatcherを作る事ができる。
/// 　Matcherには演算子オーバーロードが設定されているので、
/// 　|演算子で選択、*演算子で回数指定、+演算子で連結を表現できる。
/// 　MatcherインスタンスのSearch()関数に入力文字列を与えれば、
/// 　検索結果として内部に木構造を持つMatchインスタンスを返す。
/// 
/// ソースコード解析を前提とした設計
///   空白・改行を無視したマッチングもできるし、
///   空白・改行を意図的に含むマッチングもできる。
/// 　インデント・デデントの検出も可能で、オフサイドルール構文にも対応する。
/// 
/// _()関数
/// 　文字型と文字列型に設定した拡張メソッド。
/// 　文字型のものは、文字に一致するMatcherを返す。
/// 　文字列型のものは、文字列に一致するMatcherを返す。
/// 　LIMEの演算子オーバーロードはMatcher型に設定してあるので、
/// 　この_()関数がLIMEの起点となる。
/// 　
/// Matcher.Not プロパティ
/// 　任意の１文字・単語区切りの否定を表現する Matcher インスタンスを返す。
/// 
/// Matcher型
/// 　検索すべき文字列パターンを表現する型。
/// 　文字１個や行末・行頭などを表現するインスタンスが最小単位で、
/// 　後述する演算子で組み合わせる事でプログラミング言語のソースコード
/// 　に合致するパターンまで表現する事ができる。
/// 　作者の想定ではC++のソースでも構文解析できるはず。
/// 
/// |演算子
/// 　Matcher同士の選択を表現できる。
/// 　糖衣構文として文字型や文字列型とも演算できる。
/// 　
/// 　// 最初の演算のオペランドがMatcher型なら、
/// 　// 他のオペランドは文字型・文字列型で良い。
/// 　var numeric = 
/// 　    '0'._() | '1' | '2' | '3' | 
/// 　    '4' | '5' | '6' | '7' | '8' | '9';
/// 
/// *演算子
/// 　Matcherに回数指定を設定できる。
/// 　左辺はMatcher型、右辺は整数型または整数範囲型が必要。
/// 　正規表現の量指定子に相当する。
/// 　
/// 　// 既に定義済みの数値を連続させたら数値列
/// 　var numerics = numeric * new Range(1, Int32.MaxValue);
/// 
/// +演算子
/// 　Matcher同士の連結ができる。
/// 　糖衣構文として文字型や文字列型とも演算できる。
/// 　
/// 　// 実数リテラルの例
/// 　var float =
/// 　    // 整数部
/// 　    ('+'._() | '-' | "") + numerics +
/// 　    // 小数部
/// 　    '.' + numerics + 
/// 　    // 指数部
/// 　    ( ('e' | 'E') + ('+' | '-' | "") + numerics );
/// 　
/// 整数範囲型
/// 　new Range(810, 114514) のように下限値と上限値を設定して生成する。
/// 　0以上の整数を与える事が可能。
/// 　C#の整数型では無限大を扱う事ができない為、
/// 　引数にInt32.MinValueを与える運用を想定している。
/// 　
/// Search()関数
/// 　Matcherのメンバ関数で、引数として文字列を取り、
/// 　発見した文字列を格納するMatchを返す。
/// 
/// Match型
/// 　検索結果を格納する型。
/// 
/// [] インデクサ
/// 　「タグ」を付与された Matcher を生成して返す。
/// 　インデクサの使い方としては著しく間違っている。
/// 　Matcher型のインデクサは、保持している子要素を取得する為ではなく、
/// 　Matcherにタグを与えたインスタンスを取得する為に存在する。
/// 　タグを与えたMatcherが作り出したMatchは同じタグを持つ。
/// 　
/// 　Match型インスタンスのインデクサにタグを与えて取得すると、
/// 　そのタグを持つMatchが列挙される。
/// 　
/// 　
/// 
/// 　
/// </summary>
namespace LIME
{
    #region 文字列からマッチャーへ変換する拡張関数群
    public static class StringEx
    {
        
        /// <summary>
        /// 指定した単語に合致するマッチャーを返す。
        /// </summary>
        /// <param name="word">単語</param>
        /// <returns></returns>
        public static Matcher _(this string word)
        {
            if (word.Length == 0)
            {
                return new ZeroLengthMatcher();
            }
            else
            {
                Matcher result = new CharMatcher(word[0]);
                for(int i = 1; i < word.Length; i++)
                {
                    result += word[i];
                }
                return result;
            }
        }

        /// <summary>
        /// 指定した文字に合致するマッチャーを返す。
        /// </summary>
        /// <param name="c">文字</param>
        /// <returns></returns>
        public static CharMatcher _(this char c)
        {
            return new CharMatcher(c);
        }

        /// <summary>
        /// 指定した文字セットのどれか１文字に合致するマッチャーを返す。
        /// </summary>
        /// <param name="chars">文字セット</param>
        /// <returns></returns>
        public static CharMatcher _(this IEnumerable<char> chars)
        {
            return new CharMatcher(chars);
        }

        public static CharMatcher To(this char min, char max)
        {
            return new CharMatcher(min, max);
        }

        public static LongMatcher AsLong(this char c)
        {
            return c._().Above1;
        }
    }
    #endregion
    
    #region 文字型の拡張メソッド
    public static class CharEx
    {
        private static HashSet<char> _wordHeadChars = new HashSet<char>
                    ("0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");

        /// <summary>
        /// 単語の先頭文字として適切な文字であるかを取得する。
        /// </summary>
        /// <param name="c">判定対象文字</param>
        /// <returns>判定結果</returns>
        public static bool IsWordHeadChar(this char c)
        {
            return _wordHeadChars.Contains(c);
        }

        /// <summary>
        /// 単語の先頭文字として適切な文字であるかを取得する。
        /// </summary>
        /// <param name="c">判定対象文字</param>
        /// <returns>判定結果</returns>
        public static bool IsWordChar(this char c)
        {
            return _wordHeadChars.Contains(c) || c == '_';
        }

        
    }
    #endregion

    public static class ArrayEx
    {
        public static T[] GetCopy<T>(this T[] array)
        {
            var length = array.Length;
            var result = new T[length];
            for(int i = 0; i < length; i++)
            {
                result[i] = array[i];
            }
            return result;
        }
        public static T[] GetCopy<T>(this T[] array, Func<T,T> func)
        {
            var length = array.Length;
            var result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = func(array[i]);
            }
            return result;
        }
        public static T[] Add<T>(this T[] array, T item)
        {
            var length = array.Length;
            var result = new T[length + 1];
            for (int i = 0; i < length; i++)
            {
                result[i] = array[i];
            }
            result[length] = item;
            return result;
        }
        public static T[] Add<T>(this T[] array, T[] items)
        {
            var length = array.Length;
            var itemLength = items.Length;

            var result = new T[length + itemLength];
            for (int i = 0; i < length; i++)
            {
                result[i] = array[i];
            }
            for (int i = 0; i <  itemLength; i++)
            {
                result[length + i] = items[i];
            }
            return result;
        }
        public static T[] Add<T>(this T a, T[] items)
        {
            var itemLength = items.Length;

            var result = new T[1 + itemLength];
            result[0] = a;
            for (int i = 0; i <  itemLength; i++)
            {
                result[1+i] = items[i];
            }
            return result;
        }
    }

    #region タグ付きマッチからの文字列の取得
    public static class MatchListEx
    {
        /// <summary>
        /// マッチの列から文字列の列に変換する。
        /// </summary>
        /// <param name="matches">マッチの列</param>
        /// <returns></returns>
        public static List<string> ToStringList(this List<Match> matches)
        {
            var result = new List<string>();

            foreach(var match in matches)
            {
                result.Add(match.Value);
            }

            return result;
        }

        /// <summary>
        /// 文字列の列を連結する。
        /// </summary>
        /// <param name="words">文字列の列</param>
        /// <returns></returns>
        public static string Join(this List<string> words)
        {
            var sb = new StringBuilder();

            foreach(var word in words)
            {
                sb.Append(word);
            }
            return sb.ToString();
        }

        /// <summary>
        /// マッチの列を単一の文字列に変換する。
        /// </summary>
        /// <param name="matches">マッチの列</param>
        /// <returns></returns>
        public static string Join(this List<Match> matches)
        {
            var sb = new StringBuilder();
            foreach (var match in matches)
            {
                sb.Append(match.Value);
            }
            return sb.ToString();
        }
    }
    #endregion

    #region 範囲型
    /// <summary>
    /// 範囲型
    /// </summary>
    public class Range
    {
        public static Range One
        {
            get { return new Range(1, 1); }
        }

        private int _min;
        public int Min { get { return _min; } }
        private int _max;
        public int Max { get { return _max; } }

        public Range(int min, int max)
        {
            if (min > max) { throw new ArgumentOutOfRangeException(); }
            _min = min;
            _max = max;
        }

        public static Range operator *(Range matcher1, Range matcher2)
        {
            return new Range(matcher1.Min * matcher2.Min, matcher1.Max * matcher2.Max);
        }
        public static Range operator *(Range matcher1, int count)
        {
            return new Range(matcher1.Min * count, matcher1.Max * count);
        }

        /// <summary>
        /// 数値から暗黙的に変換できるようにしておく。
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Range(int value)
        {
            if (value < 0) { throw new ArgumentOutOfRangeException(); }
            return new Range(value, value);
        }
    }

    #endregion

    #region EasyDictionary

    /// <summary>
    /// キーが無い場合でもエラーを吐かずに自動的にキーを作って値を格納する簡単辞書
    /// </summary>
    /// <typeparam name="TKey1"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class EasyDictionary1<TKey1, TValue>
    {
        private Dictionary<TKey1, TValue> _dict
            = new Dictionary<TKey1, TValue>();

        public TValue this[TKey1 key]
        {
            get
            {
                return _dict[key];
            }
            set
            {
                if (_dict.ContainsKey(key) == false)
                {
                    _dict.Add(key, value);
                }
                else
                {
                    _dict[key] = value;
                }
            }
        }
        public bool ContainsKey(TKey1 key1)
        {
            return _dict.ContainsKey(key1);
        }

        public void Clear()
        {
            _dict.Clear();
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                return _dict.Values;
            }
        }
    }

    public class EasyDictionary2<TKey2, TKey1, TValue>
    {
        private Dictionary<TKey2, EasyDictionary1<TKey1, TValue>> _dict
            = new Dictionary<TKey2, EasyDictionary1<TKey1, TValue>>();
        public EasyDictionary1<TKey1, TValue> this[TKey2 key2]
        {
            get
            {
                if (_dict.ContainsKey(key2) == false)
                {
                    _dict.Add(key2, new EasyDictionary1<TKey1, TValue>());
                }
                return _dict[key2];
            }
        }
        public TValue this[TKey2 key2, TKey1 key1]
        {
            get
            {
                return this[key2][key1];
            }
            set
            {
                _dict[key2][key1] = value;
            }
        }

        public bool ContainsKey(TKey2 key2, TKey1 key1)
        {
            if (_dict.ContainsKey(key2) == false) { return false; }
            if (_dict[key2].ContainsKey(key1) == false) { return false; }
            return true;
        }

        public void Clear()
        {
            var keys = _dict.Keys;
            foreach(var key in keys)
            {
                _dict[key].Clear();
            }
            _dict.Clear();
        }
        public IEnumerable<TValue> Values
        {
            get
            {
                foreach(var innerDict in _dict.Values)
                {
                    foreach(var value in innerDict.Values)
                    {
                        yield return value;
                    }
                }
            }
        }
    }

    public class EasyDictionary3<TKey3, TKey2, TKey1, TValue>
    {
        private Dictionary<TKey3, EasyDictionary2<TKey2, TKey1, TValue>> _dict
            = new Dictionary<TKey3, EasyDictionary2<TKey2, TKey1, TValue>>();
        public EasyDictionary2<TKey2, TKey1, TValue> this[TKey3 key3]
        {
            get
            {
                if (_dict.ContainsKey(key3) == false)
                {
                    _dict.Add(key3, new EasyDictionary2<TKey2, TKey1, TValue>());
                }
                return _dict[key3];
            }
        }
        public TValue this[TKey3 key3, TKey2 key2, TKey1 key1]
        {
            get
            {
                return this[key3][key2][key1];
            }
            set
            {
                _dict[key3][key2][key1] = value;
            }
        }

        public bool ContainsKey(TKey3 key3, TKey2 key2, TKey1 key1)
        {
            if (_dict.ContainsKey(key3) == false) { return false; }
            if (_dict[key3].ContainsKey(key2, key1) == false) { return false; }
            return true;
        }

        public void Clear()
        {
            var keys = _dict.Keys;
            foreach (var key in keys)
            {
                _dict[key].Clear();
            }
            _dict.Clear();
        }
        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (var innerDict in _dict.Values)
                {
                    foreach (var value in innerDict.Values)
                    {
                        yield return value;
                    }
                }
            }
        }
    }

    public class EasyDictionary4<TKey4, TKey3, TKey2, TKey1, TValue>
    {
        private Dictionary<TKey4, EasyDictionary3<TKey3, TKey2, TKey1, TValue>> _dict
            = new Dictionary<TKey4, EasyDictionary3<TKey3, TKey2, TKey1, TValue>>();
        public EasyDictionary3<TKey3, TKey2, TKey1, TValue> this[TKey4 key4]
        {
            get
            {
                if (_dict.ContainsKey(key4) == false)
                {
                    _dict.Add(key4, new EasyDictionary3<TKey3, TKey2, TKey1, TValue>());
                }
                return _dict[key4];
            }
        }
        public TValue this[TKey4 key4, TKey3 key3, TKey2 key2, TKey1 key1]
        {
            get
            {
                return this[key4][key3][key2][key1];
            }
            set
            {
                _dict[key4][key3][key2][key1] = value;
            }
        }

        public bool ContainsKey(TKey4 key4, TKey3 key3, TKey2 key2, TKey1 key1)
        {
            if (_dict.ContainsKey(key4) == false) { return false; }
            if (_dict[key4].ContainsKey(key3, key2, key1) == false) { return false; }
            return true;
        }
        public void Clear()
        {
            var keys = _dict.Keys;
            foreach (var key in keys)
            {
                _dict[key].Clear();
            }
            _dict.Clear();
        }
        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (var innerDict in _dict.Values)
                {
                    foreach (var value in innerDict.Values)
                    {
                        yield return value;
                    }
                }
            }
        }
    }
    #endregion

    #region 文字列範囲型
    public struct TextRange: IComparable<TextRange>
    {
        private static int _uniqIDSorce = 0;

        public string UniqID { get; private set; }

        public int Begin;
        public int End;

        public TextRange(int begin, int end)
        {
            UniqID = "R" + _uniqIDSorce.ToString();
            _uniqIDSorce++;

            Begin = begin;
            End = end;
        }

        public int Length
        {
            get { return End - Begin; }
        }


        public int CompareTo(TextRange other)
        {
            return Begin - other.Begin;
        }

        public override string ToString()
        {
            return String.Format("[{0}-{1}]", Begin,End);
        }
    }
    #endregion

    #region 空白カウンター
    /// <summary>
    /// 空白カウンター
    /// </summary>
    public class SpaceCounter
    {
        private int _begin;
        private int _end;
        private bool _isRunning = false;

        /// <summary>
        /// 空白かも知れない文字を入力して受理したか否かを返信する
        /// </summary>
        /// <param name="currentIndex">現在のインデックス</param>
        /// <param name="currentChar">現在の文字</param>
        /// <returns>
        /// 空白を受け入れたら true 。空白では無い時は false
        /// </returns>
        public bool AddSpace(int currentIndex, char currentChar)
        {
            // 空白・タブの時
            if((currentChar == ' ')|| (currentChar == '\t'))
            {
                // 空白の開始を検知していなかった時
                if (_isRunning == false)
                {
                    // 開始位置を設定する
                    _begin = currentIndex;
                    _end = _begin;
                    _isRunning = true;
                }

                // 終了位置を加算
                _end++;
                return true;
            }

            return false;
        }

        public int SpaceLength
        {
            get
            {
                if(_isRunning == false)
                {
                    return 0;
                }
                return _end - _begin; 
            }
        }

        public TextRange CurrentRange
        {
            get
            {
                return new TextRange(_begin, _end);
            }
        }

        public void Reset()
        {
            _isRunning = false;
        }
    }
    #endregion

    #region トークンストリーム
    /// <summary>
    /// 文字列からトークン列に変換するストリーム
    /// </summary>
    /// <remarks>
    /// 改行、
    /// 
    /// </remarks>
    public class TokenStream : IEnumerable<TokenStream.TokenRange>
    {
        private IEnumerable<char> _text;

        /// <summary>
        /// トークン列の元になる文字列を指定するコンストラクタ
        /// </summary>
        /// <param name="text">トークン列の元になる文字列</param>
        public TokenStream(IEnumerable<char> text)
        {
            _text = text;
        }


        public IEnumerator<TokenRange> GetEnumerator()
        {
            foreach (var result in InsertZeroLength
                (InsertWordBreak
                (InsertIndent
                (CompoundNewLine
                (CompoundSpace(_text))))))
            {
                yield return result;
            }

            //List<TokenRange> list;

            //list = new List<TokenRange>(CompoundSpace(_text));
            //list = new List<TokenRange>(CompoundNewLine(list));
            //list = new List<TokenRange>(InsertIndent(list));
            //list = new List<TokenRange>(InsertWordBreak(list));
            //list = new List<TokenRange>(InsertZeroLength(list));

            //foreach (var item in list)
            //{
            //    yield return item;
            //}
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            IEnumerator<TokenRange> result = GetEnumerator();
            return result;
        }

        private static SortedSet<char> _wordChars =
            new SortedSet<char>("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");
        
        /// <summary>
        /// 文字列からトークンに変換する
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private TokenKind CharToToken(char c)
        {
            if (_wordChars.Contains(c))
            {
                return TokenKind.WordChar;
            }

            switch (c)
            {
            case ' ':
            case '\t':
                return TokenKind.Space;
            case '\r':
                return TokenKind.Cr;
            case '\n':
                return TokenKind.Lf;
            default:
                return TokenKind.OneChar;
            }
        }

        /// <summary>
        /// 生テキストをトークン列に変換し、
        /// 開始・終了トークンを追加し、
        /// スペースを SpaceArray としてまとめる
        /// </summary>
        /// <param name="text">生テキスト</param>
        /// <returns></returns>
        private IEnumerable<TokenRange> CompoundSpace(IEnumerable<char> text)
        {
            int index = -1;
            int charCount = 0;
            bool isSpaceBegining = false;
            int spaceBegin = -999;
            int spaceEnd = -999;

            yield return new TokenRange
                (TokenKind.Begin, charCount, charCount);
            foreach (var c in text)
            {
                charCount++;
                index = charCount - 1;

                var atom = CharToToken(c);
                if(atom == TokenKind.Space)
                {
                    if(isSpaceBegining == false)
                    {
                        spaceBegin = index;
                        spaceEnd = spaceBegin + 1;
                        isSpaceBegining = true;
                    }
                    else
                    {
                        spaceEnd++;
                    }
                }
                else
                {
                    if(isSpaceBegining)
                    {
                        // この時点でスペースの長さを確定してしまう
                        yield return new TokenRange
                            (TokenKind.SpaceArray, spaceBegin, spaceEnd);
                        isSpaceBegining = false;
                    }
                    yield return new TokenRange(atom, index, index + 1);
                }
            }
            if (isSpaceBegining)
            {
                // 未完成のまま文字列が終わってしまったスペースの長さを確定してしまう
                yield return new TokenRange(TokenKind.SpaceArray, spaceBegin, spaceEnd);
            }

            yield return new TokenRange
                (TokenKind.End, charCount, charCount);
        }

        /// <summary>
        /// トークン列に含まれる Cr と Lf の連続を CrLf に変換する
        /// </summary>
        /// <param name="atoms"></param>
        /// <returns></returns>
        private IEnumerable<TokenRange> CompoundNewLine(IEnumerable<TokenRange> atoms)
        {
            bool prevIsCr = false;
            int prevEnd = 0;

            // 文字列先頭に行頭を差し込む
            yield return new TokenRange
                (TokenKind.LineHead, 0, 0);

            foreach (var atomRange in atoms)
            {
                var atom = atomRange.Kind;

                if (prevIsCr)
                {
                    prevIsCr = false;
                    if (atom == TokenKind.Lf)
                    {
                        // Lf をそのまま返す
                        yield return atomRange;

                        // Lf 直後に行頭を差し込む
                        yield return new TokenRange
                            (TokenKind.LineHead, atomRange.End, atomRange.End);
                        
                        // 次の文字の処理に移る
                        continue;
                    }
                    else
                    {
                        // １文字前の Cr 直後に行頭を差し込む
                        yield return new TokenRange
                            (TokenKind.LineHead, prevEnd, prevEnd);
                    }
                }

                // 現在文字が Cr ならフラグを設定する
                if (atom == TokenKind.Cr)
                {
                    prevIsCr = true;
                    prevEnd = atomRange.End;

                }
                // 来たトークンをそのまま返す
                yield return atomRange;
            }
        }

        /// <summary>
        /// インデント・デデントを示すトークンを挿入する。
        /// </summary>
        /// <param name="atoms"></param>
        /// <returns></returns>
        private IEnumerable<TokenRange> InsertIndent(IEnumerable<TokenRange> atoms)
        {
            TokenRangeBuffer buff = new TokenRangeBuffer(atoms);

            Stack<int> levels = new Stack<int>();
            levels.Push(0);

            bool IsTextChar(TokenRange range)
            {
                switch(range.Kind)
                {
                case TokenKind.WordChar:
                case TokenKind.OneChar:
                    return true;
                }

                return false;
            }

            // バッファを満たしながらループ
            while (buff.BeFull() > 0)
            {
                // 行頭・スペース・文字　の時
                if ((buff.Count == 3) &&
                    ((buff[0].Kind == TokenKind.LineHead) || 
                    (buff[0].Kind == TokenKind.Begin)) &&
                    (buff[1].Kind == TokenKind.SpaceArray) &&
                    (IsTextChar(buff[2])))
                {
                    // スペースの長さを取得しておく
                    var spaceLength = buff[1].Length;
                    // スペースの終了座標
                    var spaceEnd = buff[1].End;

                    // LineHead は捨てる
                    buff.Peek();

                    // 空白を返信する
                    yield return buff.Peek();


                    // 更にインデントされた時
                    if (levels.Peek() < spaceLength)
                    {
                        levels.Push(spaceLength);
                        yield return new TokenRange
                            (TokenKind.Indent, spaceEnd, spaceEnd);
                    }
                    // デデントを計算する必要がある時
                    else if (levels.Peek() > spaceLength)
                    {
                        int dedentCoount;
                        bool dedentError;
                        CountDedent(ref levels, spaceLength, out dedentCoount, out dedentError);

                        while(dedentCoount > 0)
                        {
                            dedentCoount--;
                            yield return new TokenRange
                                (TokenKind.Dedent, spaceEnd, spaceEnd);
                        }
                        if(dedentError)
                        {
                            yield return new TokenRange
                                (TokenKind.DedentError, spaceEnd, spaceEnd);
                        }
                    }

                    // 文字を返信する
                    yield return buff.Peek();
                }

                // 行頭・文字　の時
                else if ((buff.Count >= 2) &&
                    (buff[0].Kind == TokenKind.LineHead) &&
                    IsTextChar(buff[1]))
                {
                    // 改行の終了位置を取得しておく
                    var spaceEnd = buff[0].End;

                    // LineHead は捨てる
                    buff.Peek();

                    // 字下げゼロ文字としてデデントを計算する
                    var spaceLength = 0;
                    int dedentCoount;
                    bool dedentError;
                    CountDedent(ref levels, spaceLength, out dedentCoount, out dedentError);

                    while (dedentCoount > 0)
                    {
                        dedentCoount--;
                        yield return new TokenRange
                            (TokenKind.Dedent, spaceEnd, spaceEnd);
                    }
                    if (dedentError)
                    {
                        yield return new TokenRange
                            (TokenKind.DedentError, spaceEnd, spaceEnd);
                    }

                    // 文字を返信する
                    yield return buff.Peek();
                }

                // その他の組み合わせの時
                else
                {
                    var temp = buff.Peek();
                    if(temp.Kind != TokenKind.LineHead)
                    {
                        // 先頭のトークン１個だけ返信する
                        yield return temp;
                    }

                }
            }
        }

        /// <summary>
        /// デデントを数える
        /// </summary>
        /// <param name="levels">インデントレベルを格納するスタック</param>
        /// <param name="spaceLength">空白の長さ</param>
        /// <param name="dedentCount">(出力)デデントの個数</param>
        /// <param name="dedentError">(出力)デデントエラーの有無</param>
        private void CountDedent
            (ref Stack<int> levels, int spaceLength, 
            out int dedentCount, out bool dedentError)
        {
            dedentError = false;

            var levelsCount = levels.Count;
            while (true)
            {
                if (levels.Peek() == spaceLength)
                {
                    break;
                }

                if (levels.Peek() < spaceLength)
                {
                    levels.Push(spaceLength);
                    dedentError = true;
                    break;
                }

                if (levels.Peek() > spaceLength)
                {
                    levels.Pop();
                }
            }

            dedentCount = levelsCount - levels.Count;
        }

        /// <summary>
        /// トークンを３個まで溜めるバッファ
        /// </summary>
        private class TokenRangeBuffer
        {
            private const int Capacity = 3;

            private List<TokenRange> _buff =
                new List<TokenRange>(Capacity);
            IEnumerator<TokenRange> _enumerator;

            public TokenRangeBuffer(IEnumerable<TokenRange> sorce)
            {
                _enumerator = sorce.GetEnumerator();
            }

            public TokenRange this[int index]
            {
                get { return _buff[index]; }
            }

            public int BeFull()
            {
                while(_buff.Count < Capacity)
                {
                    if(_enumerator.MoveNext() == false)
                    {
                        break;
                    }
                    Push(_enumerator.Current);
                }
                return _buff.Count;
            }

            public bool IsFull
            {
                get { return _buff.Count == Capacity; }
            }

            public int Count
            {
                get { return _buff.Count; }
            }

            public void Push(TokenRange value)
            {
                if(_buff.Count < Capacity)
                {
                    _buff.Add(value);
                }
                else
                {
                    Shift();
                    _buff.Add(value);
                }
            }

            private void Shift()
            {
                var lastIndex = _buff.Count - 1;

                for (int i = 0; i < lastIndex; i++)
                {
                    _buff[i] = _buff[i + 1];
                }
                if (lastIndex >= 0)
                {
                    _buff.RemoveAt(lastIndex);
                }
            }

            public TokenRange Peek()
            {
                if(_buff.Count == 0) { throw new IndexOutOfRangeException(); }

                var result = _buff[0];
                Shift();
                return result;
            }
        }

        /// <summary>
        /// トークン列に WordBreak を挿入する
        /// </summary>
        /// <param name="atoms"></param>
        /// <returns></returns>
        private IEnumerable<TokenRange> InsertWordBreak(IEnumerable<TokenRange> atoms)
        {
            bool prevIsWordChar = false;
            int lastEnd = -999;
            foreach (var atomRange in atoms)
            {
                var atom = atomRange.Kind;
                lastEnd = atomRange.End;
                if ((prevIsWordChar == false) && (atom == TokenKind.WordChar))
                {
                    yield return new TokenRange
                        (TokenKind.WordBegin, atomRange.Begin, atomRange.Begin);
                    prevIsWordChar = true;
                }
                else if (prevIsWordChar && (atom != TokenKind.WordChar))
                {
                    yield return new TokenRange
                        (TokenKind.WordEnd, atomRange.Begin, atomRange.Begin);
                    prevIsWordChar = false;
                }
                yield return atomRange;
            }
            if(prevIsWordChar)
            {
                yield return new TokenRange
                    (TokenKind.WordEnd, lastEnd, lastEnd);
            }
        }

        /// <summary>
        /// トークン列に長さゼロトークンを挿入する
        /// </summary>
        /// <param name="atoms"></param>
        /// <returns></returns>
        private IEnumerable<TokenRange> InsertZeroLength(IEnumerable<TokenRange> atoms)
        {
            TokenRange prevToken = TokenRange.Invalid;

            foreach (var atomRange in atoms)
            {
                var kind = atomRange.Kind;
                var prevKind = prevToken.Kind;

                if (prevKind == TokenKind._invalid_)
                {
                    yield return atomRange;
                    prevToken = atomRange;
                    continue;
                }
                // 長さゼロトークンを差し込まなければならない場合とは？
                // 単語開始の後
                // 単語終了の前
                // 文字と文字の間
                // 空白と文字の間
                // 改行と文字の間
                // 文字と空白の間
                // 文字と改行の間

                switch (kind)
                {
                case TokenKind.OneChar:
                case TokenKind.WordChar:
                    yield return new TokenRange
                        (TokenKind.ZeroLength, atomRange.Begin, atomRange.Begin);
                    break;
                case TokenKind.WordEnd:
                case TokenKind.SpaceArray:
                case TokenKind.End:
                    if( (prevKind == TokenKind.OneChar) ||
                        (prevKind == TokenKind.WordChar))
                    {
                        yield return new TokenRange
                            (TokenKind.ZeroLength, atomRange.Begin, atomRange.Begin);
                    }
                    break;
                }
                yield return atomRange;
                prevToken = atomRange;
            }
        }
        public enum TokenKind
        {
            /// <summary>(無効)</summary>
            _invalid_,
            /// <summary>文字列先頭</summary>
            Begin,
            /// <summary>(中間処理用)キャリッジリターン</summary>
            Cr,
            /// <summary>(中間処理用)ラインフィード</summary>
            Lf,
            /// <summary>行頭</summary>
            LineHead,
            /// <summary>(中間処理用)空白１個</summary>
            Space,
            /// <summary>１個以上の空白</summary>
            SpaceArray,
            /// <summary>(中間処理用)単語用文字</summary>
            WordChar,
            /// <summary>単語の始まり</summary>
            WordBegin,
            /// <summary>単語の終わり</summary>
            WordEnd,
            /// <summary>文字１個</summary>
            OneChar,
            /// <summary>インデント</summary>
            Indent,
            /// <summary>デデント</summary>
            Dedent,
            /// <summary>デデントエラー</summary>
            DedentError,
            /// <summary>長さゼロ</summary>
            ZeroLength,
            /// <summary>文字列終端</summary>
            End
        }

        

        public struct TokenRange
        {
            private static int _uniqIdBase = 0;
            public string UniqID { get; private set; }

            public TokenKind Kind { get; private set; }

            public int Begin { get; private set; }
            public int End { get; private set; }

            public int Length
            {
                get { return End - Begin; }
            }

            public TokenRange(TokenKind kind, int begin, int end)
            {
                Kind = kind;
                Begin = begin;
                End = end;

                UniqID = "T" + _uniqIdBase.ToString();
                _uniqIdBase++;
            }

            public TextRange Range
            {
                get { return new TextRange(Begin, End); }
            }

            public override string ToString()
            {
                return string.Format("[{0}-{1}] {2} {3}", Begin, End, Kind, UniqID);
            }

            private static TokenRange _invalid
                = new TokenRange(TokenKind._invalid_, -999, -999);
            public static TokenRange Invalid
            {
                get { return _invalid; }
            }
        }
        
    }
    #endregion

    #region ユニークインデックスジェネレーター
    /// <summary>
    /// ユニークインデックスジェネレーター
    /// </summary>
    public static class UniqIndexGenarator
    {
        private static Dictionary<Type, Dictionary<object, int>> _idTable
            = new Dictionary<Type, Dictionary<object, int>>();

        /// <summary>
        /// このインスタンスに割り当てられた型ごとに独立したユニークインデックスを返す
        /// </summary>
        /// <param name="obj">このインスタンス</param>
        /// <returns>インスタンスに割り当てられたユニークインデックス</returns>
        public static int UniqIndex(this object obj, Type type)
        {
            var objType = type;

            Dictionary<object, int> dict;
            if(_idTable.ContainsKey(objType) == false)
            {
                dict = new Dictionary<object, int>();
                _idTable.Add(objType, dict);
            }
            else
            {
                dict = _idTable[objType];
            }

            if(dict.ContainsKey(obj) == false)
            {
                var dictCount = dict.Count;
                dict.Add(obj, dictCount);
                return dictCount;
            }
            else
            {
                return dict[obj];
            }
            
        }
    }
    #endregion
}
