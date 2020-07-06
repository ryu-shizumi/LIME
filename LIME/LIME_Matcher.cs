using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LIME
{
    ///<summary>
    /// Matcher 
    /// 
    /// 
    /// 
    /// 
    /// </summary>


    #region 文字入力を受け付けるインターフェイス
    /// <summary>
    /// 走査文字位置が進む度にOnChar()が実行される。
    /// </summary>
    public interface IReceiveChar
    {
        // 内部で必要に応じて親マッチャーのOnPresentedを呼び出す。
        Match ReceiveChar(Executor executor, int index);
    }
    #endregion

    #region 長さゼロでマッチするインターフェイス
    public interface IZeroLength
    {
        // 内部で必要に応じて親マッチャーのOnPresentedを呼び出す。
        Match OnZeroLength(Executor executor, int index);
    }
    #endregion

    #region 内包要素を持つマッチャーのインターフェイス
    // 内包要素を持ち、内包要素からマッチの進入を受ける。
    public interface IHasInner
    {
        /// <summary>
        /// 内包要素で作られたマッチを受け取る。
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="innerMatch">内包要素から上がってきたマッチ</param>
        Match[] ReceiveMatch(Executor executor, Match innerMatch);

        string ToString(HashSet<RecursionMatcher> hash);

        IEnumerable<Matcher> Inners { get; }
    }
    #endregion

    #region 内包要素を持つマッチャー
    /// <summary>
    /// 内包要素を持つマッチャー
    /// </summary>
    public abstract class HasInnerMatcher : Matcher, IHasInner
    {
        /// <summary>
        /// 内包要素で作られたマッチを受け取る。
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="innerMatch">内包要素から上がってきたマッチ</param>
        public virtual Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            return innerMatch.ToArray();
        }

        public override string ToString()
        {
            var hash = new HashSet<RecursionMatcher>();
            return ToString(hash);
        }
        public abstract string ToString(HashSet<RecursionMatcher> hash);

        public abstract IEnumerable<Matcher> Inners { get; }
    }
    #endregion

    #region マッチャー(演算可能な最小単位)
    /// <summary>
    /// マッチャー(演算可能な最小単位)
    /// </summary>
    /// <remarks>
    /// 
    /// 高速化の為の「分身」
    /// 
    /// マッチャーにはオリジナルと分身体の区別がある。
    /// 
    /// マッチャーは演算されてより大きなマッチャーになる。
    /// 大きなマッチャーに取り込まれるのは元マッチャーの分身体。
    /// 
    /// 例えば 'a'._() + 'b'._() という演算が行われて
    /// 文字列"ab"に一致するマッチャーが作られる場合、
    /// "ab" に取り込まれた 'a' は元の 'a' の分身体。
    /// 'b' も元の 'b' の分身体である。
    /// 
    /// 更に "ab" + 'c' と演算される場合、
    /// "ab" の分身が作られるに伴い、内包された 'a' 'b' も分身が作られる。
    /// 
    /// 演算で大きなマッチャーがどんどん作られ、
    /// オペランドとなったマッチャーはどんどん分身する。
    /// 
    /// 演算の度に「分身体の分身体」「分身体の分身体の分身体」とどんどん増える。
    /// 但し、分身体には「第一世代分身体」「第二世代分身体」「第三世代分身体」
    /// などといった区別は全く無い。
    /// 
    /// 'a' に一致するマッチャーがオリジナル１体、分身９体が存在しても、
    /// マッチング動作を行うのはオリジナルのみで、マッチを生成するのもオリジナルのみ。
    /// 但し、発せられたマッチはそれぞれの分身がそれぞれの所属する大きなマッチャーに通知される。
    /// この場合は分身９体がそれぞれマッチング動作を行う演算量を削減できる。
    /// 
    /// </remarks>
    public abstract class Matcher
    {
        public string UniqID { get; private set; }
        private static int _uniqNumber = 0;
        public Matcher()
        {
            UniqID = $"G{_uniqNumber}";
            _uniqNumber++;
        }
        public static void ResetUniqID()
        {
            _uniqNumber = 0;
        }

        public string TypeName
        {
            get
            {
                var type = this.GetType();
                var typeName = type.Name.Replace(type.Namespace, "")
                    .Replace("Matcher","");
                return typeName;
            }
        }

#if DEBUG
        public string ToTreeText()
        {
            var sb = new StringBuilder();
            var hash = new HashSet<RecursionMatcher>();

            ToTreeText(sb, "", hash);

            return sb.ToString();
        }
        private void ToTreeText(
            StringBuilder sb, string header, HashSet<RecursionMatcher> hash)
        {
            if (this is RecursionMatcher recurs)
            {
                if (hash.Contains(recurs))
                {
                    return;
                }
                else
                {
                    hash.Add(recurs);
                }
            }

            var name = this.GetType().Name.Replace("Matcher", "");
            var uniqID = "";
            if (this.IsOriginal)
            {
                uniqID = this.UniqID;
            }
            else
            {
                uniqID = this.UniqID + "(" + this.Original.UniqID + ")";
            }
            sb.AppendLine(header + name + " " + uniqID);

            if (this is HasInnerMatcher hasInner)
            {
                foreach (var inner in hasInner.Inners)
                {
                    inner.ToTreeText(sb, header + "  ", hash);
                }
            }
        }

        public string ToTreeText(Executor executor)
        {
            var sb = new StringBuilder();
            var hash = new HashSet<RecursionMatcher>();

            ToTreeText(executor, sb, "", hash);

            return sb.ToString();
        }
        private void ToTreeText(Executor executor, StringBuilder sb, string header, HashSet<RecursionMatcher> hash)
        {
            if (this is RecursionMatcher recurs)
            {
                if (hash.Contains(recurs))
                {
                    return;
                }
                else
                {
                    hash.Add(recurs);
                }
            }

            var name = this.TypeName;
            var uniqID = "";
            if (this.IsOriginal)
            {
                uniqID = this.UniqID;
            }
            else
            {
                uniqID = this.UniqID + "(" + this.Original.UniqID + ")";
            }
            var targetChar = "";
            if(this is CharMatcher charMatcher)
            {
                targetChar = charMatcher.ToString();
            }

            var stayings = executor.Staying_PosToMatch(this.Original);
            var stayIdArray = new string[stayings.Length];
            for (int i = 0; i < stayings.Length; i++)
            {
                stayIdArray[i] = "停" + stayings[i].UniqID;
            }
            var runings = executor.Running_PosToMatch(this.Original);
            var runIdArray = new string[runings.Length];
            for (int i = 0; i < runings.Length; i++)
            {
                runIdArray[i] = "走" + runings[i].UniqID;
            }
            var matches = "";
            if ((stayings.Length != 0) || (runings.Length != 0))
            {
                matches = " " + string.Join(" ", stayIdArray) + " " +
                    string.Join(" ", runIdArray);
            }

            var MatcherStateText = $"{header} {name} {uniqID} {targetChar} {matches}";
            sb.AppendLine(MatcherStateText);

            if (this is HasInnerMatcher hasInner)
            {
                foreach (var inner in hasInner.Inners)
                {
                    inner.ToTreeText(executor, sb, header + "  ", hash);
                }
            }
        }
#endif

        protected Matcher _original = null;
        public Matcher Original
        {
            get
            {
                if (_original != null)
                {
                    return _original;
                }
                else
                {
                    return this;
                }
            }
            set
            {
                _original = value;
            }
        }
        public bool IsOriginal
        {
            get { return _original == null; }
        }

        #region デバッグ用名前付け

        private string _debugName = null;
        public string DebugName
        {
            get
            {
                return _debugName;
            }

            set
            {
                _debugName = value;
            }
        }
        #endregion

        public override string ToString()
        {
            if (this._debugName != null)
            {
                return this._debugName;
            }

            return base.ToString();
        }

        #region 演算子オーバーロード(加算)
        /// <summary>
        /// 加算演算子は連結マッチャーを返す。
        /// </summary>
        /// <param name="a">左オペランド</param>
        /// <param name="b">右オペランド</param>
        /// <returns>連結マッチャー</returns>
        public static PairMatcher operator +(Matcher a, Matcher b)
        {
            if ((a == null) || (b == null))
            {
                throw new ArgumentNullException();
            }

            var copyA = a.GetCopy();
            var copyB = b.GetCopy();
            var result = new PairMatcher(copyA, copyB);

            return result;
        }


        public static PairMatcher operator +(Matcher a, string word)
        {
            if ((a == null) || (word == null))
            {
                throw new ArgumentNullException();
            }

            return a + word._();
        }

        public static PairMatcher operator +(Matcher a, char c)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }

            return a + c._();
        }

        public static PairMatcher operator +(string word, Matcher a)
        {
            if ((word == null) || (a == null))
            {
                throw new ArgumentNullException();
            }

            return word._() + a;
        }

        public static PairMatcher operator +(char c, Matcher a)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }

            return c._() + a;
        }
        #endregion

        #region 演算子オーバーロード(論理和)
        /// <summary>
        /// 論理和演算子は選択マッチャーを返す。
        /// </summary>
        /// <param name="a">左オペランド</param>
        /// <param name="b">右オペランド</param>
        /// <returns>連結マッチャー</returns>
        public static EitherMatcher operator |(Matcher a, Matcher b)
        {
            if ((a == null) || (b == null))
            {
                throw new ArgumentNullException();
            }

            var result = new EitherMatcher(a.GetCopy(), b.GetCopy());

            return result;
        }

        public static EitherMatcher operator |(Matcher a, string word)
        {
            if ((a == null) || (word == null))
            {
                throw new ArgumentNullException();
            }

            return a | word._();
        }

        public static EitherMatcher operator |(Matcher a, char c)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }

            return a | c._();
        }

        public static EitherMatcher operator |(string word, Matcher a)
        {
            if ((word == null) || (a == null))
            {
                throw new ArgumentNullException();
            }

            return word._() | a;
        }

        public static EitherMatcher operator |(char c, Matcher a)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }

            return c._() | a;
        }



        #endregion

        #region 演算子オーバーロード(乗算)
        public static LoopMatcher operator *(Matcher a, Range r)
        {
            if ((a == null) || (r == null))
            {
                throw new ArgumentNullException();
            }
            LoopMatcher result;
            LoopMatcher loopA = a as LoopMatcher;

            if (loopA == null)
            {
                result = new LoopMatcher(a.GetCopy(), r.Min, r.Max);
            }
            else
            {
                result = new LoopMatcher(loopA.GetCopy(), loopA.Min * r.Min, loopA.Max * r.Max);
            }
            return result;
        }

        public static LoopMatcher operator *(Matcher a, int times)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }

            LoopMatcher result;

            if (a is LoopMatcher loopA)
            {
                result = new LoopMatcher(loopA, loopA.Min * times, loopA.Max * times);
            }
            else
            {
                result = new LoopMatcher(a, times, times);
            }

            return result;
        }
        #endregion

        #region 演算子オーバーロード(等価比較)
        public static bool operator ==(Matcher m, object other)
        {
            object o = (object)m;
            return o == other;
        }
        public static bool operator !=(Matcher m, object other)
        {
            object o = (object)m;
            return !(o == other);
        }



        public override bool Equals(System.Object obj)
        {
            if (obj is string text)
            {
                return false;
            }

            return (object)this == obj;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        #endregion

        #region タグ付け
        /// <summary>
        /// タグの付いたマッチャーのインスタンスを返す。
        /// </summary>
        /// <param name="tag">タグ</param>
        /// <returns></returns>
        public virtual Matcher this[string tag]
        {
            get
            {
                return new CaptureMatcher(this, tag);
            }
        }
        #endregion

        #region 回数指定(できる限り長く)
        /// <summary>
        /// １回以上かつ、できる限り長く
        /// </summary>
        public LongMatcher Above1
        {
            get
            {
                if (this is LongMatcher)
                {
                    return (LongMatcher)(this.GetCopy());
                }

                return new LongMatcher(this.GetCopy());
            }
        }
        /// <summary>
        /// ０回以上かつ、できる限り長く
        /// </summary>
        public EitherMatcher Above0
        {
            get
            {
                if (this is LongMatcher)
                {
                    return "" | this;
                }

                return "" | new LongMatcher(this.GetCopy());
            }
        }
        #endregion

        #region 回数指定
        /// <summary>
        /// 0回または1回にマッチするマッチャーに変換する。
        /// </summary>
        /// <returns></returns>
        public Matcher _01()
        {
            return (this | "");
        }

        /// <summary>
        /// 回数を指定した繰り返しを設定する。デリミタを挟み込む事もできる。
        /// </summary>
        /// <param name="min">最小繰り返し回数</param>
        /// <param name="max">最大繰り返し回数</param>
        /// <param name="delimiter">デリミタ</param>
        /// <returns></returns>
        public Matcher Times(int min, int max, Matcher delimiter = null)
        {
            if (max < min)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (delimiter == null)
            {
                return new LoopMatcher(this, min, max);
            }
            else
            {
                if (max == 0)
                {
                    // 最大回数がゼロなら、全体は長さゼロマッチ
                    return ""._();
                }
                else if (max == 1)
                {
                    // 最大回数が１回なら後半部分は何も無くなる
                    return this;
                }
                else
                {
                    if (min == 0)
                    {
                        return "" | (this + (delimiter + new LoopMatcher(this, 0, max - 1)));
                    }
                    else if (min == 1)
                    {
                        return this + (delimiter + new LoopMatcher(this, 0, max - 1));
                    }
                    else
                    {
                        return this + (delimiter + new LoopMatcher(this, min - 1, max - 1));
                    }
                }
            }

        }

        /// <summary>
        /// 回数を指定した繰り返しを設定する。デリミタを挟み込む事もできる。
        /// </summary>
        /// <param name="count">繰り返し回数</param>
        /// <param name="delimiter">デリミタ</param>
        /// <returns></returns>
        public Matcher Times(int count, Matcher delimiter = null)
        {
            return this.Times(count, count, delimiter);
        }

        /// <summary>
        /// 0～int.MaxValue回にマッチするマッチャーに変換する。
        /// </summary>
        /// <returns></returns>
        public Matcher _0Max()
        {
            return Times(0, int.MaxValue);
        }
        /// <summary>
        /// 0～int.MaxValue回にマッチするマッチャーに変換する。挟み込むデリミタを指定できる。
        /// </summary>
        /// <param name="delimiter">デリミタ</param>
        /// <returns></returns>
        public Matcher _0Max(Matcher delimiter)
        {
            return Times(0, int.MaxValue, delimiter);
        }
        /// <summary>
        /// 0～int.MaxValue回にマッチするマッチャーに変換する。挟み込むデリミタを指定できる。
        /// </summary>
        /// <param name="delimiter">デリミタ</param>
        /// <returns></returns>
        public Matcher _0Max(char delimiter)
        {
            return Times(0, int.MaxValue, delimiter._());
        }
        /// <summary>
        /// 0～int.MaxValue回にマッチするマッチャーに変換する。挟み込むデリミタを指定できる。
        /// </summary>
        /// <param name="delimiter">デリミタ</param>
        /// <returns></returns>
        public Matcher _0Max(string delimiter)
        {
            return Times(0, int.MaxValue, delimiter._());
        }

        /// <summary>
        /// 1～int.MaxValue回にマッチするマッチャーに変換する。
        /// </summary>
        /// <returns></returns>
        public Matcher _1Max()
        {
            return Times(1, int.MaxValue);
        }
        /// <summary>
        /// 1～int.MaxValue回にマッチするマッチャーに変換する。挟み込むデリミタを指定できる。
        /// </summary>
        /// <param name="delimiter">デリミタ</param>
        /// <returns></returns>
        public Matcher _1Max(Matcher delimiter)
        {
            return Times(1, int.MaxValue, delimiter);
        }
        /// <summary>
        /// 1～int.MaxValue回にマッチするマッチャーに変換する。挟み込むデリミタを指定できる。
        /// </summary>
        /// <param name="delimiter">デリミタ</param>
        /// <returns></returns>
        public Matcher _1Max(char delimiter)
        {
            return Times(1, int.MaxValue, delimiter._());
        }
        /// <summary>
        /// 1～int.MaxValue回にマッチするマッチャーに変換する。挟み込むデリミタを指定できる。
        /// </summary>
        /// <param name="delimiter">デリミタ</param>
        /// <returns></returns>
        public Matcher _1Max(string delimiter)
        {
            return Times(1, int.MaxValue, delimiter._());
        }

        #endregion 回数指定



        #region 検索処理
        /// <summary>
        /// このマッチャーで文字列末尾まで検索し、
        /// 最も早く出現し、かつその内で最も長いマッチを返す。
        /// マッチが得られない時は null を返す。
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public RootMatch Search(IEnumerable<char> text)
        {
            // 実行器を作成する。
            Executor exe = new Executor(this, text);

            bool matchExists = false;

            // １文字ずつ入力を消費して返信を得る。
            foreach (var exeResult in exe)
            {
                matchExists = true;
            }

            if (matchExists == false)
            {
                return null;
            }

            // 最も早く出現し、かつその内で最も長いマッチを返す。
            return Match.SelectBestMatch(exe.FinishedMatches);
        }

        #endregion




        /// <summary>
        /// 直下の子要素を列挙する。孫以降は列挙しない。
        /// </summary>
        public virtual IEnumerable<Matcher> EnumInner()
        {
            return new Matcher[] { };
        }

        /// <summary>
        /// このインスタンス直下の枝(IHasInner)要素を列挙する。
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IHasInner> EnumBranch()
        {
            List<IHasInner> result = new List<IHasInner>();

            foreach (var inner in EnumInner())
            {
                if (inner is IHasInner hasInner)
                {
                    result.Add(hasInner);
                }
            }
            return result;
        }

        /// <summary>
        /// このインスタンス直下の葉(IHasInnerでは無い)要素を列挙する。
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Matcher> EnumLeaf()
        {
            List<Matcher> result = new List<Matcher>();

            foreach (var inner in EnumInner())
            {
                IHasInner hasInner = inner as IHasInner;
                if (hasInner == null)
                {
                    result.Add(inner);
                }
            }
            return result;
        }

        public abstract Matcher GetCopy();
    }


    #endregion マッチャー(演算可能な最小単位)

    #region 組み込みマッチャー
    /// <summary>
    /// よく使いそうな「文字列」「数字列」「行区切り」などの
    /// 組み込み定数的なマッチャーを提供する。
    /// </summary>
    public abstract class BuiltInMatcher
    {
        /// <summary>
        /// 文字列先頭
        /// </summary>
        public static Matcher Begin
        {
            get
            {
                return new BeginMatcher();
            }
        }

        #region 文字列先頭マッチャー
        /// <summary>
        /// 文字列先頭マッチャー
        /// </summary>
        public class BeginMatcher : NotableMatcher, IZeroLength
        {
            #region 否定フラグ
            /// <summary>
            /// 否定フラグを格納するプライベートフィールド
            /// </summary>
            private bool _not = false;

            /// <summary>
            /// １文字の否定を表現するマッチャーを返す。
            /// </summary>
            public override NotableMatcher Not
            {
                get
                {
                    return new BeginMatcher(!(this._not));
                }
            }
            #endregion

            public BeginMatcher()
            {
            }

            public BeginMatcher(bool not)
            {
                _not = not;
            }

            public Match OnZeroLength(Executor executor, int index)
            {
                bool success = true;

                if (this._not)
                {
                    success = !(success);
                }

                // 非マッチの時は何もしないで処理終了。
                if (success == false)
                {
                    return null;
                }

                Match newMatch = new BeginMatch(executor, this);
                return newMatch;
            }

            public override string ToString()
            {
                if (_not)
                {
                    return "(!BEGIN)";
                }
                else
                {
                    return "(BEGIN)";
                }
            }



            public override Matcher GetCopy()
            {
                return GetNotableCopy();
            }

            public override NotableMatcher GetNotableCopy()
            {
                var result = new BeginMatcher(_not);
                result.Original = Original;
                return result;
            }

        }
        #endregion

        /// <summary>
        /// 文字列末尾
        /// </summary>
        public static Matcher End
        {
            get
            {
                return new EndMatcher();
            }
        }

        #region 文字列末尾マッチャー
        /// <summary>
        /// 文字列末尾マッチャー
        /// </summary>
        public class EndMatcher : NotableMatcher, IZeroLength
        {
            #region 否定フラグ
            /// <summary>
            /// 否定フラグを格納するプライベートフィールド
            /// </summary>
            private bool _not = false;

            /// <summary>
            /// １文字の否定を表現するマッチャーを返す。
            /// </summary>
            public override NotableMatcher Not
            {
                get
                {
                    return new EndMatcher(!(this._not));
                }
            }
            #endregion

            public EndMatcher()
            {
            }
            public EndMatcher(bool not)
            {
                _not = not;
            }


            public override string ToString()
            {
                if (_not)
                {
                    return "(!END)";
                }
                else
                {
                    return "(END)";
                }
            }

            public Match OnZeroLength(Executor executor, int index)
            {
                bool success = true;

                if (this._not)
                {
                    success = !(success);
                }

                // 非マッチの時は何もしないで処理終了。
                if (success == false)
                {
                    return null;
                }

                var t = executor.Text;

                var newMatch = new EndMatch(executor, index, this);
                return newMatch;
            }



            public override Matcher GetCopy()
            {
                return GetNotableCopy();
            }

            public override NotableMatcher GetNotableCopy()
            {
                var result = new EndMatcher(_not);
                result.Original = Original;
                return result;
            }

        }
        #endregion

        /// <summary>
        /// 単語区切り(否定可能)
        /// </summary>
        public static NotableMatcher WordBreak
        {
            get
            {
                return new WordBreakMatcher();
            }
        }

        #region 単語区切りマッチャー
        /// <summary>
        /// 単語区切りマッチャー
        /// </summary>
        public class WordBreakMatcher : NotableMatcher, IZeroLength
        {
            #region 否定フラグ
            /// <summary>
            /// 否定フラグを格納するプライベートフィールド
            /// </summary>
            private bool _not = false;

            /// <summary>
            /// １文字の否定を表現するマッチャーを返す。
            /// </summary>
            public override NotableMatcher Not
            {
                get
                {
                    return new WordBreakMatcher(!(this._not));
                }
            }
            #endregion

            #region ToString
            /// <summary>
            /// このインスタンスが指し示すパターンを文字列化する。
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                if (this._not)
                {
                    return "\\B";
                }
                else
                {
                    return "\\b";
                }
            }
            #endregion


            public WordBreakMatcher(bool not = false)
            {
                this._not = not;
            }

            public Match OnZeroLength(Executor executor, int index)
            {
                // 非マッチの時は何もしないで処理終了。
                if (this._not)
                {
                    return null;
                }

                Match newMatch = new ZeroLengthMatch(executor, index, this);
                return newMatch;
            }

            public override Matcher GetCopy()
            {
                return GetNotableCopy();
            }

            public override NotableMatcher GetNotableCopy()
            {
                var result = new WordBreakMatcher(_not);
                result.Original = Original;
                return result;
            }

        }
        #endregion

        /// <summary>
        /// 前の行を超える量の字下げに反応するマッチャー
        /// </summary>
        public static Matcher Indent
        {
            get
            {
                return new IndentMatcher();
            }
        }

        #region インデントマッチャー
        public class IndentMatcher : Matcher
        {
            public override Matcher GetCopy()
            {
                var result = new IndentMatcher();
                result.Original = Original;
                return result;
            }

            public Match CreateMatch(Executor executor, int begin, int end)
            {
                Match newMatch = new IndentMatch(executor, begin, end, this);
                return newMatch;
            }
        }
        #endregion

        public static Matcher Dedent
        {
            get
            {
                return new DedentMatcher();
            }
        }
        #region デデントマッチャー
        public class DedentMatcher : Matcher
        {
            public override Matcher GetCopy()
            {
                var result = new DedentMatcher();
                result.Original = Original;
                return result;
            }

            public Match CreateMatch(Executor executor, int begin, int end)
            {
                Match newMatch = new DedentMatch(executor, begin, end, this);
                return newMatch;

            }
        }
        #endregion

        public static Matcher ErrorDedent
        {
            get
            {
                return new ErrorDedentMatcher();
            }
        }
        #region エラーデデントマッチャー
        public class ErrorDedentMatcher : Matcher
        {
            public override Matcher GetCopy()
            {
                var result = new DedentMatcher();
                result.Original = Original;
                return result;
            }

            public Match CreateMatch(Executor executor, int begin, int end)
            {
                Match newMatch = new ErrorDedentMatch(executor, begin, end, this);
                return newMatch;

            }
        }
        #endregion


    }
    #endregion

    #region キャプチャーマッチャー
    /// <summary>
    /// キャプチャー指定を示すマッチャー。
    /// </summary>
    /// <remarks>
    /// このマッチャーが持つ番号は生成されたマッチに与えられ、
    /// インデクサで番号を指定してサブマッチにアクセスできる。
    /// </remarks>
    public class CaptureMatcher : Matcher, IHasInner
    {
        private Matcher _inner;
        public string Tag { get; private set; }
        public IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _inner;
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="inner">内包要素</param>
        /// <param name="captureID">キャプチャーID</param>
        public CaptureMatcher(Matcher inner, string tag)
        {
            this._inner = inner;
            // inner.Parent = this;
            this.Tag = tag;

            this.DebugName = "TagMatcher";
        }

        public string ToString(HashSet<RecursionMatcher> hash)
        {
            if (this.DebugName != null) { return this.DebugName; }

            if (_inner is IHasInner i)
            {
                return i.ToString(hash);
            }
            else
            {
                return _inner.ToString();
            }
        }

        /// <summary>
        /// 内包要素からマッチの提出を受け、
        /// 必要に応じて親マッチャーにマッチを提出する。
        /// </summary>
        /// <param name="innerMatch">内包要素から上がってきたマッチ</param>
        public Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            var tagMatch = new CaptureMatch(executor, innerMatch, (CaptureMatcher)(this.Original), Tag);

            return tagMatch.ToArray();
        }

        /// <summary>
        /// 内包要素を列挙する。
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Matcher> EnumInner()
        {
            return new Matcher[] { _inner };
        }

        public override Matcher GetCopy()
        {
            var result = new CaptureMatcher(_inner.GetCopy(), Tag);
            result.Original = Original;
            return result;
        }

    }
    #endregion

    #region BufferedEnumerator
    public class BufferedEnumerator<T> : IList<T>, IEnumerator<T>
    {
        private IEnumerator<T> _enum;

        private IList<T> _list;

        private int _index = -1;

        private bool _isFinished = false;

        public bool IsFinished { get => _isFinished; }

        public BufferedEnumerator(IEnumerable<T> items)
        {
            _enum = items.GetEnumerator();
        }
        public BufferedEnumerator(IEnumerator<T> items)
        {
            _enum = items;
        }

        public int CurrentIndex
        {
            get => _index;
        }

        public T this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        public int Count => _list.Count;

        public bool IsReadOnly => true;

        public void Add(T item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item) => _list.Contains(item);

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public int IndexOf(T item) => _list.IndexOf(item);

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }



        public T Current => _enum.Current;

        object IEnumerator.Current => _enum.Current;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            Func<bool> f = ((IEnumerator)this).MoveNext;

            _enum.Reset();
            var newEnum = new IEnumeratorWraper
                (
                    () => { return _enum.Current; },
                    MoveNext,
                    () => { _enum.Dispose(); },
                    ((IEnumerator)this).Reset
                );

            return newEnum;
        }


        public bool MoveNext()
        {
            // リストの有効性を取得する。
            var _isValid = _enum.MoveNext();

            if (_isValid)
            {
                // 内部インデックスをインクリメントする。
                _index++;

                if (_list == null)
                {
                    _list = new List<T>();
                }
                _list.Add(_enum.Current);
            }
            else
            {
                // 内部インデックスをインクリメントする。
                _index++;

                _isFinished = true;
            }

            return _isValid;
        }

        public void Reset()
        {
            _enum.Reset();
            _list = null;

            _index = -1;
            _isFinished = false;
        }

        /// <summary>
        /// 列挙子のラッパークラス
        /// </summary>
        public class IEnumeratorWraper : IEnumerator<T>
        {
            public IEnumeratorWraper
                (
                    Func<T> funcCurrent,
                    Func<bool> funcMoveNext,
                    Action funcDispose,
                    Action funcReset
                )
            {
                _funcCurrent = funcCurrent;
                _funcMoveNext = funcMoveNext;
                _funcDispose = funcDispose;
                _funcReset = funcReset;
            }

            private Func<T> _funcCurrent;
            private Func<bool> _funcMoveNext;
            private Action _funcDispose;
            private Action _funcReset;


            T IEnumerator<T>.Current => _funcCurrent();

            object IEnumerator.Current => _funcCurrent();

            void IDisposable.Dispose() => _funcDispose();

            bool IEnumerator.MoveNext() => _funcMoveNext();

            void IEnumerator.Reset() => _funcReset();
        }


    }
    #endregion

    #region 否定可能マッチャー
    /// <summary>
    /// 否定可能マッチャー
    /// </summary>
    public abstract class NotableMatcher : Matcher
    {
        /// <summary>
        /// 否定フラグを反転させたインスタンスを取得する。
        /// </summary>
        public abstract NotableMatcher Not { get; }


        #region 演算子オーバーロード(論理和)
        /// <summary>
        /// 論理和演算子は否定可能選択マッチャーを返す。
        /// </summary>
        /// <param name="a">左オペランド</param>
        /// <param name="b">右オペランド</param>
        /// <returns>連結マッチャー</returns>
        public static EitherNotableMatcher operator |(NotableMatcher a, NotableMatcher b)
        {
            if ((a == null) || (b == null))
            {
                throw new ArgumentNullException();
            }

            var result = new EitherNotableMatcher(a, b);
            return result;
        }

        public static EitherNotableMatcher operator |(NotableMatcher a, char c)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }

            return a | c._();
        }



        public static EitherNotableMatcher operator |(char c, NotableMatcher a)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }

            return c._() | a;
        }
        #endregion

        public abstract NotableMatcher GetNotableCopy();
    }
    #endregion

    #region マッチャーのルート
    /// <summary>
    /// マッチャーのルート
    /// </summary>
    /// <remarks>
    /// 
    /// 全てのマッチャーのルート(親ノード)として設定され、
    /// パターンマッチング処理は、
    /// 末端ノードからマッチが発生し、このノードで終わる。
    /// 
    /// マッチがこのノードに達すれば、
    /// そのマッチはマッチングを全うしたという事である。
    /// 
    /// </remarks>
    public class RootMatcher : HasInnerMatcher
    {
        private Matcher _inner;
        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _inner;
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="inner">内包するマッチャー</param>
        public RootMatcher(Matcher inner)
        {
            this._inner = inner;
            // inner.Parent = this;
        }

        public override string ToString()
        {
            if (this.DebugName != null) { return this.DebugName; }

            return "(Root)";
        }

        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            if (this.DebugName != null) { return this.DebugName; }

            if (_inner is IHasInner i)
            {
                return i.ToString(hash);
            }
            else
            {
                return _inner.ToString();
            }
        }

        /// <summary>
        /// 子マッチャーからマッチを報告された時
        /// </summary>
        /// <param name="executor"></param>
        /// <param name="innerMatch"></param>
        public override Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            var rootMatch = new RootMatch(executor, innerMatch, (RootMatcher)(this.Original));

            return rootMatch.ToArray();
            //executor.SetMatchPosition(rootMatch, this);
        }



        /// <summary>
        /// 内包要素を列挙する。
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Matcher> EnumInner()
        {
            return new Matcher[] { _inner };
        }

        public override Matcher GetCopy()
        {
            var result = new RootMatcher(_inner.GetCopy());
            result.Original = Original;
            return result;
        }
    }
    #endregion

    #region １文字マッチャー
    /// <summary>
    /// １文字マッチャー
    /// </summary>
    public class CharMatcher : NotableMatcher, IReceiveChar
    {
        //private CharComparer _comparer;
        private CharRange[] _inners;

        public bool IsNot { get; private set; }

        #region ToString
        /// <summary>
        /// このインスタンスが指し示すパターンを文字列化する。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (this.DebugName != null) { return this.DebugName; }

            if (_inners.Length == 0)
            {
                return "";
            }
            else if (_inners.Length == 1)
            {
                if (_inners[0] is CharRangeSimple simple)
                {
                    if (IsNot)
                    {
                        return string.Format("[^{0}]", simple.ToString());
                    }
                    else
                    {
                        return simple.ToString();
                    }
                }
                else
                {
                    var minmax = (CharRangeMinMax)(_inners[0]);
                    if (IsNot)
                    {
                        return string.Format("[^{0}-{1}]", minmax.Min, minmax.Max);
                    }
                    else
                    {
                        return string.Format("[{0}-{1}]", minmax.Min, minmax.Max);
                    }
                }
            }
            else
            {
                var sb = new StringBuilder();

                sb.Append("[");
                if (IsNot)
                {
                    sb.Append("^");
                }

                foreach (var inner in _inners)
                {
                    sb.Append(inner.ToString());
                }

                sb.Append("]");
                return sb.ToString();
            }
        }
        #endregion

        #region 糖衣構文コンストラクタ

        /// <summary>
        /// 文字一つを指定するコンストラクタ
        /// </summary>
        /// <param name="c">文字</param>
        /// <param name="not">否定フラグ(デフォルトは否定しない)</param>
        public CharMatcher(char c, bool not = false)
        {
            _inners = new CharRange[1];
            _inners[0] = new CharRangeSimple(c);
            IsNot = not;

            //_comparer = SimpleComparer.GetInstance(c, not);
        }

        /// <summary>
        /// 文字範囲を指定するコンストラクタ
        /// </summary>
        /// <param name="min">文字範囲の最小</param>
        /// <param name="max">文字範囲の最大</param>
        /// <param name="not">否定フラグ(デフォルトは否定しない)</param>
        public CharMatcher(char min, char max, bool not = false)
        {
            _inners = new CharRange[1];
            _inners[0] = new CharRangeMinMax(min, max);
            IsNot = not;

            //_comparer = new RangeComparer(min, max, not);
        }
        /// <summary>
        /// 文字セットを指定するコンストラクタ
        /// </summary>
        /// <param name="chars">ヒットと判定させたい文字セット</param>
        /// <param name="not">否定フラグ(デフォルトは否定しない)</param>
        public CharMatcher(IEnumerable<char> chars, bool not = false)
        {
            var list = new List<CharRangeSimple>();
            foreach (var c in chars)
            {
                list.Add(new CharRangeSimple(c));
            }

            _inners = list.ToArray();
            IsNot = not;
        }


        #endregion

        public CharMatcher(CharRange[] inners, bool not = false)
        {
            _inners = inners;
            IsNot = not;
        }

        #region 演算子オーバーロード(論理和)
        /// <summary>
        /// 論理和演算子は否定可能選択マッチャーを返す。
        /// </summary>
        /// <param name="a">左オペランド</param>
        /// <param name="b">右オペランド</param>
        /// <returns>連結マッチャー</returns>
        public static CharMatcher operator |(CharMatcher a, CharMatcher b)
        {
            if ((a == null) || (b == null))
            {
                throw new ArgumentNullException();
            }

            if (a.IsNot != b.IsNot)
            {
                throw new ArgumentException("[x]と[^x]は論理和で結合できません。");
            }

            return new CharMatcher(a._inners.Add(b._inners), a.IsNot);
        }
        public static CharMatcher operator |(CharMatcher a, char c)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }
            return new CharMatcher(a._inners.Add(new CharRangeSimple(c)), a.IsNot);
        }
        public static CharMatcher operator |(char c, CharMatcher a)
        {
            if (a == null)
            {
                throw new ArgumentNullException();
            }

            return new CharMatcher((new CharRangeSimple(c)).Add(a._inners), a.IsNot);
        }




        #endregion

        /// <summary>
        /// １文字の否定を表現するマッチャーを返す。
        /// </summary>
        public override NotableMatcher Not
        {
            get
            {
                return new CharMatcher(_inners.GetCopy(), !IsNot);
            }
        }

        /// <summary>
        /// 入力を１文字与えられた時の処理
        /// </summary>
        /// <param name="executor">実行器</param>
        public Match ReceiveChar(Executor executor, int index)
        {
            var text = executor.Text;
            char c = text[index];
            bool matched;

            if (IsNot)
            {
                matched = true;

                // 否定されていれば、要素のどれかに合致してはいけない
                foreach (var inner in _inners)
                {
                    if (inner.IsMatch(c))
                    {
                        matched = false;
                        break;
                    }
                }
            }
            else
            {
                matched = false;

                // 否定されてなければ、要素の内どれかであれば良い
                foreach (var inner in _inners)
                {
                    if (inner.IsMatch(c))
                    {
                        matched = true;
                        break;
                    }
                }
            }

            if (matched)
            {
                return new CharMatch(executor, index, this);
            }
            else
            {
                return null;
            }
        }


        public override Matcher GetCopy()
        {
            return GetNotableCopy();
        }
        public override NotableMatcher GetNotableCopy()
        {
            var result = new CharMatcher(_inners, IsNot);
            result.Original = Original;
            return result;
        }
    }

    #endregion

    #region 文字範囲
    public interface ICompareChar
    {
        bool IsMatch(char c);
    }

    public abstract class CharRange : ICompareChar
    {
        public abstract char Min { get; }
        public abstract char Max { get; }

        public abstract bool IsMatch(char c);

        public abstract CharRange GetCopy();
    }

    public class CharRangeSimple : CharRange, ICompareChar
    {
        private char _c;
        public override char Min
        {
            get { return _c; }
        }
        public override char Max
        {
            get { return _c; }
        }

        public CharRangeSimple(char c)
        {
            _c = c;
        }
        public override bool IsMatch(char c)
        {
            return _c == c;
        }
        public override string ToString()
        {
            if (_c == '\r') { return "\\r"; }
            if (_c == '\n') { return "\\n"; }
            return _c.ToString();
        }
        public override CharRange GetCopy()
        {
            return new CharRangeSimple(_c);
        }
    }


    public class CharRangeMinMax : CharRange, ICompareChar
    {
        private char _min;
        private char _max;
        public override char Min
        {
            get { return _min; }
        }
        public override char Max
        {
            get { return _max; }
        }
        public CharRangeMinMax(char min, char max)
        {
            _min = min;
            _max = max;
        }

        public override bool IsMatch(char c)
        {
            if (c < _min) { return false; }
            if (_max < c) { return false; }
            return true;
        }
        public override string ToString()
        {
            return string.Format("{0}-{1}", _min, _max);
        }
        public override CharRange GetCopy()
        {
            return new CharRangeMinMax(_min, _max);
        }
    }
    #endregion

    #region 長さゼロマッチャー
    public class ZeroLengthMatcher : Matcher, IZeroLength
    {
        public Match OnZeroLength(Executor executor, int index)
        {
            // 長さゼロのマッチを生成する。
            var newMatch = new ZeroLengthMatch(executor, index, this);
            return newMatch;
        }

        public override string ToString()
        {
            return "\"\"";
        }

        public override Matcher GetCopy()
        {
            var result = new ZeroLengthMatcher();
            result.Original = Original;
            return result;
        }
    }
    #endregion

    #region ロングマッチャーの先頭に立つマッチャー
    public class LongHeadMatcher : HasInnerMatcher
    {
        private Matcher _inner;

        public LongHeadMatcher(Matcher inner)
        {
            _inner = inner;
        }
        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _inner;
            }
        }

        public override Matcher GetCopy()
        {
            return new LongHeadMatcher(_inner.GetCopy());
        }

        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            if (this.DebugName != null) { return this.DebugName; }

            string innerString = null;
            if (_inner is IHasInner i)
            {
                innerString = i.ToString(hash);
            }
            else
            {
                innerString = _inner.ToString();
            }

            return innerString;
        }
        public override Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            var longHeadMatch = new LongHeadMatch(executor, innerMatch, this);
            return longHeadMatch.ToArray();
        }
    }
    #endregion

    #region ロングマッチャー
    /// <summary>
    /// 最長一致するマッチャー
    /// </summary>
    public class LongMatcher : HasInnerMatcher
    {
        private Matcher _inner;

        public Matcher Body
        {
            get { return _inner; }
        }

        public LongMatcher(Matcher body)
        {
            _inner = body;
        }

        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _inner;
            }
        }

        public override Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            if(innerMatch is CaptureMatch cap)
            {
                if( (cap.Tag == "rule") && (innerMatch.TextLength == 10))
                {
                    Debug.WriteLine($"c = {innerMatch.ToString(executor.Text)}");
                }
            }

            if (innerMatch.ToString(executor.Text) == "a")
            {
                var temp = "";
            }

            var stayMatches = executor.Staying_PosToMatch(this.Original);

            var result = new List<Match>();

            if (stayMatches.Length == 0)
            {
                var longMatch = new LongMatch(executor, innerMatch, this);
                result.Add(longMatch);
            }
            else
            {
                //
                // 跳躍マッチが指す最長一致マッチに新規要素を接続する処理
                //
                foreach (Match stayMatch in stayMatches)
                {
                    var jumpMatch = (JumpMatch)stayMatch;

                    var longMatch = jumpMatch.JumpTarget;

                    // 文字範囲が連続しているマッチの時
                    if (executor.IsMatchContinuing(longMatch, innerMatch))
                    {
                        longMatch.Add(executor, innerMatch);
                    }
                }
            }

            return result.ToArray();
        }

        public override Matcher GetCopy()
        {
            var innerCopy = _inner?.GetCopy();
            var result = new LongMatcher(innerCopy);
            result.Original = Original;
            return result;
        }

        #region ToString
        /// <summary>
        /// このインスタンスが指し示すパターンを文字列化する。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (this.DebugName != null) { return this.DebugName; }

            return this.ToString(new HashSet<RecursionMatcher>());
        }

        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            if (this.DebugName != null) { return this.DebugName; }
            StringBuilder sb = new StringBuilder();

            sb.Append("(");
            string innerString = null;
            if (_inner is IHasInner i)
            {
                innerString = i.ToString(hash);
            }
            else
            {
                innerString = _inner.ToString();
            }

            sb.Append(innerString);
            sb.Append(")");

            sb.Append("{");
            sb.Append("Long");
            sb.Append("}");
            sb.Append(" ");
            sb.Append(UniqID);

            return sb.ToString();
        }
        #endregion


    }
    #endregion

    #region ループマッチャー
    /// <summary>
    /// ループマッチャー
    /// </summary>
    /// <remarks>
    /// 素直に使うと指数関数的に計算量が増加してしまうので、
    /// 
    /// 
    /// </remarks>
    public class LoopMatcher : HasInnerMatcher, IZeroLength
    {
        private Matcher _inner;
        public Matcher Inner
        {
            get { return _inner; }
        }
        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _inner;
            }
        }


        private int _min;
        public int Min { get { return _min; } }

        private int _max;
        public int Max { get { return _max; } }

        #region ToString
        /// <summary>
        /// このインスタンスが指し示すパターンを文字列化する。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (this.DebugName != null) { return this.DebugName; }

            return this.ToString(new HashSet<RecursionMatcher>());
        }

        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            if (this.DebugName != null) { return this.DebugName; }

            string innerString = null;
            if (_inner is IHasInner i)
            {
                innerString = i.ToString(hash);
            }
            else
            {
                innerString = _inner.ToString();
            }



            StringBuilder sb = new StringBuilder();
            sb.Append(innerString);

            sb.Append("{");
            sb.Append(this._min);
            sb.Append(",");
            if (this._max == int.MaxValue)
            {
                sb.Append("∞");
            }
            else
            {
                sb.Append(this._max);
            }

            sb.Append(")");
            sb.Append("}");

            return sb.ToString();
        }
        #endregion

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="inner">内包要素</param>
        /// <param name="min">最小回数</param>
        /// <param name="max">最大回数</param>
        /// <param name="delimiter">デリミタ</param>
        public LoopMatcher(Matcher inner, int min, int max)
        {
            this._min = min;
            this._max = max;

            this._inner = inner;
        }

        public override Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            //int index = executor.CurrentIndex;
            //Console.WriteLine(index);

            var stayMatches = executor.Staying_PosToMatch(this.Original);

            var result = new List<Match>();

            //
            // 待機マッチに新規要素を接続して上に上げる処理
            //
            foreach (Match stayMatch in stayMatches)
            {
                var waitingMatch = (WaitingMatch)stayMatch;

                // 文字範囲が連続しているマッチの時
                //if (waitingMatch.TextEnd == innerMatch.TextBegin)
                if ( executor.IsMatchContinuing(waitingMatch, innerMatch))
                {
                    // 新要素が追加された状態の待機マッチを作る
                    var newWaitingMatch = waitingMatch.CreateAppendedInstance
                        (executor, innerMatch);

                    // 待機マッチの現在位置をこのマッチャーに設定する。
                    executor.Staying_SetMatchPos(newWaitingMatch, this);

                    // ループした回数を計算する。
                    var loopedCount = newWaitingMatch.InnersCount;

                    // 最小回数を超えている時
                    if (_min <= loopedCount)
                    {
                        // 上に送るループマッチを、
                        // 新要素追加済みの待機マッチのコピーとして作る
                        var loopMatch = newWaitingMatch.Copy(executor);

                        result.Add(loopMatch);
                    }

                    // 最大回数に達した時
                    if (loopedCount == _max)
                    {
                        //Debug.WriteLine("<<< RemoveWaitingMatch >>>");
                        //executor.ViewMatchTree(newWaitingMatch);

                        // 待機マッチ を消去する。
                        newWaitingMatch.UnWrap(executor);
                        //Debug.WriteLine("<<<  >>>");
                    }
                }
            }

            //
            // 内包要素からのマッチの時は、これだけで１回目と見なす解釈も有り得る。
            //
            if (true)
            {
                // 待機マッチを新たに作る
                var newWaitingMatch = new WaitingMatch(executor, innerMatch, this);

                // 待機マッチの現在位置をこのマッチャーに設定する。
                executor.Staying_SetMatchPos(newWaitingMatch, this);

                // ループした回数を計算する。
                int loopedCount = 1;

                // 最小回数を超えている時
                if (_min <= loopedCount)
                {
                    // 上に送るループマッチを作る
                    var loopMatch = newWaitingMatch.Copy(executor);

                    result.Add(loopMatch);
                }
                // 最大回数に達した時
                if (loopedCount == _max)
                {
                    // 待機マッチ を消去する。
                    newWaitingMatch.UnWrap(executor);
                }
            }

            return result.ToArray();
        }

        public Match OnZeroLength(Executor executor, int index)
        {
            // 最低回数がゼロ回のループマッチャーは、
            // 常に長さゼロでマッチした扱いになる。
            if (_min == 0)
            {
                // 長さゼロのマッチを生成する。
                Match newMatch = new ZeroLengthMatch(executor, index, this);
                return newMatch;
            }
            return null;
        }



        /// <summary>
        /// 内包要素を列挙する。
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Matcher> EnumInner()
        {
            yield return _inner;
        }

        public override Matcher GetCopy()
        {
            var innerCopy = _inner?.GetCopy();
            var result = new LoopMatcher
                (innerCopy, _min, _max);
            result.Original = Original;
            return result;
        }
    }
    #endregion

    #region 左側マッチャー
    public class LeftMatcher : HasInnerMatcher
    {
        private Matcher _inner;

        public LeftMatcher(Matcher inner)
        {
            _inner = inner;
        }

        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _inner;
            }
        }



        public override Matcher GetCopy()
        {
            var result = new LeftMatcher(_inner.GetCopy());
            result.Original = Original;
            return result;
        }

        public override Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            var leftMatch = new LeftMatch(executor, innerMatch, this);
            return leftMatch.ToArray();
        }

        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            if (_inner is HasInnerMatcher hasInner)
            {
                return hasInner.ToString(hash);
            }
            else
            {
                return _inner.ToString();
            }
        }
    }
    #endregion

    #region 右側マッチャー
    public class RightMatcher : HasInnerMatcher
    {
        private Matcher _inner;

        public RightMatcher(Matcher inner)
        {
            _inner = inner;
        }

        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _inner;
            }
        }



        public override Matcher GetCopy()
        {
            var result = new RightMatcher(_inner.GetCopy());
            result.Original = Original;
            return result;

        }

        public override Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            var rightMatch = new RightMatch(executor, innerMatch, this);
            return rightMatch.ToArray();
        }

        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            if (_inner is HasInnerMatcher hasInner)
            {
                return hasInner.ToString(hash);
            }
            else
            {
                return _inner.ToString();
            }
        }
    }
    #endregion

    #region 連結マッチャー
    /// <summary>
    /// 連結マッチャー
    /// </summary>
    public class PairMatcher : HasInnerMatcher
    {
        private LeftMatcher _innerLeft;
        private RightMatcher _innerRight;

        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _innerLeft;
                yield return _innerRight;
            }
        }

        public PairMatcher(Matcher left, Matcher right)
        {
            _innerLeft = new LeftMatcher(left);
            _innerRight = new RightMatcher(right);
        }

        public override Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            var result = new List<Match>();

            // 先頭の内包要素から上がってきた時
            if (innerMatch is LeftMatch leftMatch)
            {
                // stayMatchesの内、範囲が重複するstayMatchが有れば統合する
                // このPairマッチャーに待機してるLeftマッチ同士を統合する

                // このPairマッチャーに待機してるLeftマッチを取得する
                var leftStayMatch = (LeftMatch)(executor.Staying_FindMatch
                    (this, leftMatch.TextBegin, leftMatch.TextEnd));

                // 結合対象となるstayMatchが無い時
                if (leftStayMatch == null)
                {
                    // 上がってきたマッチの現在位置をこのマッチャーに設定する。
                    executor.Staying_SetMatchPos(leftMatch, this);
                }
                else
                {
                    // stayMatchに上がってきたマッチを統合する
                    leftStayMatch.Unit(executor, leftMatch);
                }
            }
            // ２番目の内包要素から上がってきた時
            else
            {
                RightMatch rightMatch = (RightMatch)innerMatch;
                var stayMatches = executor.Staying_PosToMatch(this);

                bool pairingSuccess = false;

                // このマッチャー上で待機しているマッチ全てと、
                // 結合の可否を確認する。
                foreach (Match stayMatch in stayMatches)
                {
                    var leftStayMatch = (LeftMatch)stayMatch;

                    //
                    // マッチ範囲の連続性を検査する。
                    //

                    // 左側の待機マッチと右側から上がってきたマッチが連続する時
                    //if (leftStayMatch.TextEnd == rightMatch.TextBegin)
                    if ( executor.IsMatchContinuing(leftStayMatch, rightMatch))
                    {
                        pairingSuccess = true;

                        var pairRunnig = executor.Running_FindMatch
                            (this, leftStayMatch.TextBegin, rightMatch.TextEnd);


                        // 左右が揃ったのでペアマッチとして包む
                        var pairMatch = new PairMatch
                            (executor, leftStayMatch, rightMatch, this);

                        result.Add(pairMatch);
                    }
                }

                // 右から上がってきたマッチに合う相手が居なかった時
                if (pairingSuccess == false)
                {
                    //Debug.WriteLine("----RemoveRightMatch----");
                    //executor.ViewMatchTree(rightMatch);

                    // 上がってきたマッチを処分する
                    rightMatch.UnWrap(executor);

                    //Debug.WriteLine("--------");
                }
            }

            return result.ToArray();
        }

        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            return _innerLeft.ToString(hash) + _innerRight.ToString(hash);
        }

        public override Matcher GetCopy()
        {
            Matcher leftInner = null;
            foreach (var inner in _innerLeft.Inners)
            {
                leftInner = inner;
            }

            Matcher rightInner = null;
            foreach (var inner in _innerRight.Inners)
            {
                rightInner = inner;
            }

            var result = new PairMatcher(leftInner.GetCopy(), rightInner.GetCopy());
            result.Original = Original;
            return result;
        }
    }

    #endregion

    #region どちらかマッチャー
    public class EitherMatcher : HasInnerMatcher
    {
        private Matcher _innerLeft;
        private Matcher _innerRight;

        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _innerLeft;
                yield return _innerRight;
            }
        }

        public EitherMatcher(Matcher left, Matcher right)
        {
            _innerLeft = left;
            _innerRight = right;
        }



        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            var leftString = "";
            if (_innerLeft is HasInnerMatcher hasInnerLeft)
            {
                leftString = hasInnerLeft.ToString(hash);
            }
            else
            {
                leftString = _innerLeft.ToString();
            }
            var rightString = "";
            if (_innerRight is HasInnerMatcher hasInnerRight)
            {
                rightString = hasInnerRight.ToString(hash);
            }
            else
            {
                rightString = _innerRight.ToString();
            }


            return leftString + "|" + rightString;
        }

        public override Matcher GetCopy()
        {
            var result = new EitherMatcher(_innerLeft.GetCopy(), _innerRight.GetCopy());
            result.Original = Original;
            return result;
        }
    }
    #endregion

    #region 否定可能どちらかマッチャー
    /// <summary>
    /// 否定可能どちらかマッチャー
    /// </summary>
    public class EitherNotableMatcher : NotableMatcher, IHasInner
    {
        private NotableMatcher _innerLeft;
        private NotableMatcher _innerRight;

        public IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _innerLeft;
                yield return _innerRight;
            }
        }

        public override NotableMatcher Not
        {
            get
            {
                return new EitherNotableMatcher(_innerLeft.Not, _innerRight.Not);
            }
        }

        public EitherNotableMatcher(NotableMatcher left, NotableMatcher right)
        {
            _innerLeft = left;
            _innerRight = right;
        }



        public Match[] ReceiveMatch(Executor executor, Match innerMatch)
        {
            return innerMatch.ToArray();

            //foreach (var parent in executor.GetParents(this.Original))
            //{
            //    // 全ての親マッチャーにマッチを報告する。
            //    parent.ReceiveMatch(executor, innerMatch);
            //}
        }

        public string ToString(HashSet<RecursionMatcher> hash)
        {
            return _innerLeft.ToString() + "|" + _innerRight.ToString();
        }

        public override Matcher GetCopy()
        {
            return GetNotableCopy();
        }

        public override NotableMatcher GetNotableCopy()
        {
            var result = new EitherNotableMatcher(_innerLeft.GetNotableCopy(), _innerRight.GetNotableCopy());
            result.Original = Original;
            return result;
        }
    }
    #endregion

    #region 再帰マッチャー
    /// <summary>
    /// 再帰的構文定義に必要な「再帰マッチャー」。
    /// 先にインスタンスを作っておいて中身は後から設定できる。
    /// </summary>
    public class RecursionMatcher : HasInnerMatcher
    {
        private Matcher _inner;
        public override IEnumerable<Matcher> Inners
        {
            get
            {
                yield return _inner;
            }
        }

        #region ToString
        /// <summary>
        /// このインスタンスが指し示すパターンを文字列化する。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (this.DebugName != null) { return this.DebugName; }

            HashSet<RecursionMatcher> hash =
                new HashSet<RecursionMatcher>();
            return ToString(hash);
        }

        /// <summary>
        /// HashSet付きToString関数。同じ再帰マッチャーを複数回通り抜けない。
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public override string ToString(HashSet<RecursionMatcher> hash)
        {
            if (this.DebugName != null) { return this.DebugName; }

            if (_inner == null)
            {
                return "(null)";
                //throw new NullReferenceException("RecursionMatcherのInnerがnullです。");
            }

            if (hash == null)
            {
                hash = new HashSet<RecursionMatcher>();
            }

            if (hash.Contains(this))
            {
                return "●";
            }
            else
            {
                hash.Add(this);

                if (_inner is IHasInner i)
                {
                    return i.ToString(hash);
                }
                else
                {
                    return _inner.ToString();
                }
            }
        }
        #endregion

        public Matcher Inner
        {
            get { return _inner; }
            set
            {
                _inner = value;
                //_inner.Parent = this;
            }
        }

        /// <summary>
        /// 再帰マッチャーを作る。
        /// </summary>
        public RecursionMatcher()
        {
            _inner = null;
        }

        /// <summary>
        /// 再帰マッチャーを作る。
        /// </summary>
        /// <param name="inner">内包要素</param>
        public RecursionMatcher(Matcher inner)
        {
            _inner = inner;
        }

        /// <summary>
        /// 内包要素を列挙する。
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Matcher> EnumInner()
        {
            return new Matcher[] { _inner };
        }

        public override Matcher GetCopy()
        {
            Matcher innerCopy;
            if (_inner == null)
            {
                innerCopy = null;
            }
            else
            {
                innerCopy = _inner.GetCopy();
            }

            var result = new RecursionMatcher(innerCopy);
            result.Original = Original;
            return result;
        }

    }
    #endregion

    
}
