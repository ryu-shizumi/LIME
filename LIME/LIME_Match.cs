using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace LIME
{
    #region Match マッチ結果
    /// <summary>
    /// マッチングの結果
    /// </summary>
    /// <remarks>
    /// 
    /// マッチとはマッチャーの木を登っていく登山者
    /// マッチは末端マッチャーから生成され、マッチャーの木を登っていく。
    /// 
    /// 
    /// マッチの開始インデックスと終了インデックスとは？
    /// 
    /// 例えば文字列 "ABC" に一致したマッチの場合
    /// "A" は 0 で始まり 1 で終わる
    /// "B" は 1 で始まり 2 で終わる
    /// "C" は 2 で始まり 3 で終わる
    /// "ABC" は 0 で始まり 3 で終わる
    /// 
    /// 終了インデックスは次要素の開始インデックスと等しくなる。
    /// 
    /// 
    /// 
    /// 全パターン網羅の為の「分身」
    /// 
    /// 
    /// 高速化の為の「統合」
    /// 
    /// 
    /// </remarks>

    public abstract class Match : IEnumerable<Match>
    {
        private static int[] stopIds = { };
        private static List<int> StopIdList = new List<int>(stopIds);
        
        public string UniqID { get; private set; }
        private static int _uniqNumber = 0;
        public Match()
        {
            UniqID = $"M{_uniqNumber}";
            _uniqNumber++;
        }
        public static void ResetUniqID()
        {
            _uniqNumber = 0;
        }


        /// <summary>
        /// このマッチを解体し、親カウントを減らす。
        /// サブマッチ全てにも同じ効果がある。
        /// </summary>
        /// <param name="executor"></param>
        public virtual void UnWrap(Executor executor)
        {
            executor.ParentsCountMinus(this);
        }

        public int ParentCount { get; set; }



        public string DebugName
        {
            get { return this.GetType().Name; }
        }

        /// <summary>
        /// このマッチ結果を生成したマッチャー
        /// </summary>
        public Matcher Generator{ get; protected set; }

        private protected List<Match> _subMatches = new List<Match>();
        /// <summary>
        /// ループでマッチした各回の要素、または、子要素列でマッチした各要素。
        /// </summary>
        public virtual IEnumerable<Match> SubMatches
        {
            get { yield break; }
        }

        /// <summary>
        /// このマッチが末端マッチか否かを取得する。
        /// </summary>
        public abstract bool IsTerminal
        { get; }



        private protected List<Match> _parents = new List<Match>();
        /// <summary>
        /// 親マッチ群
        /// </summary>
        public List<Match> Parents
        {
            get { return _parents; }
        }

        public abstract int TextBegin
        { get; }

        public abstract int TextEnd
        { get; }

        public int TextLength
        {
            get { return TextEnd - TextBegin; }
        }


        public enum ConnectionStatus
        {
            Connected,
            NotConnect
        }

        public abstract ConnectionStatus LeftConnection
        { get; }

        public abstract ConnectionStatus RightConnection
        { get; }



        #region 子要素の列挙処理



        public IEnumerator<Match> GetEnumerator()
        {
            return ((IEnumerable<Match>)SubMatches).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<Match>)SubMatches).GetEnumerator();
        }
        #endregion

        /// <summary>
        /// このマッチをマッチリストに変換する。
        /// </summary>
        /// <returns></returns>
        public MatchList ToMatchList()
        {
            return new MatchList(this.SubMatches);
        }


        #region ToString()

        public string ToString(IList<char> text)
        {
            var sb = new StringBuilder(TextEnd - TextBegin);

            for (int i = TextBegin; i < TextEnd; i++)
            {
                sb.Append(text[i]);
            }

            return sb.ToString();
        }

        public string ToString(string text)
        {
            var sb = new StringBuilder(TextEnd - TextBegin);

            for (int i = TextBegin; i < TextEnd; i++)
            {
                sb.Append(text[i]);
            }

            return sb.ToString();
        }

        #endregion

        /// <summary>
        /// Valueプロパティ
        /// </summary>
        public string Value
        {
            get
            {
                return this.Generator.ToString();
            }
        }

        /// <summary>
        /// 複数のマッチの中から最良のものを選ぶ。
        /// </summary>
        /// <param name="matches"></param>
        /// <returns>
        /// ●開始位置が早い事が最優先
        /// ●次に長さが長い事
        /// ●上記を更に絞り込む方法は未定
        /// </returns>
        public static RootMatch SelectBestMatch(List<RootMatch> matches)
        {
            int CurrentMinBegin = int.MaxValue;

            List<Match> filteredMatches = new List<Match>();

            // まずは全体を見回して最も早い開始位置を探る。
            foreach (Match match in matches)
            {
                int begin = match.TextBegin;

                // 現在開始記録より早いインデックスを見つけたら、
                // それまで見つけた要素は棄却して新しく加える。
                if (begin < CurrentMinBegin)
                {
                    filteredMatches.Clear();
                    filteredMatches.Add(match);
                    CurrentMinBegin = begin;
                }
                // 現在開始記録と等しい要素は素直に加える。
                else if (begin == CurrentMinBegin)
                {
                    filteredMatches.Add(match);
                }
                // 現在開始記録を超える要素は無視する。
                else
                {
                    continue;
                }
            }

            List<RootMatch> resultMatches = new List<RootMatch>();

            int CurrentMaxLength = -1;

            // 全体を見回して最も早い長いマッチを探る。
            foreach (RootMatch match in filteredMatches)
            {
                int length = match.TextLength;

                if (CurrentMaxLength < length)
                {
                    resultMatches.Clear();
                    resultMatches.Add(match);
                    CurrentMaxLength = length;
                }
                else if (length == CurrentMaxLength)
                {
                    resultMatches.Add(match);
                }
                else
                {
                    continue;
                }
            }

            if (resultMatches.Count == 0)
            {
                return null;
            }

            // 一番最初の要素を返す(暫定的な仕様)
            return resultMatches[0];
        }
        
        public virtual Match[] ToArray()
        {
            return new Match[1] { this };
        }

        /// <summary>
        /// このマッチと統合する別のマッチを指定して統合マッチに変化させる。
        /// 停止マッチリストに在る自分の参照を統合マッチの参照に上書きする。
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="other">統合する別のマッチ</param>
        /// <returns>統合する要素が加わった後の自分</returns>
        public virtual Match Unit(Executor executor, Match other)
        {
            // 統合マッチを作る。
            var unit = new UnitMatch(this, other);

            // このマッチの現在地を得る
            var stayPos = executor.Staying_MatchToPos(this);
            // このマッチが停止中マッチとして登録されている時
            if (stayPos != null)
            {
                // 新しい統合マッチを自分と同じ位置に置く
                executor.Staying_SetMatchPos(unit, stayPos);

                // 自分を停止マッチリストから削除する
                executor.Staying_RemoveMatch(this);
            }

            var runningPos = executor.Running_MatchToPos(this);
            // このマッチが走行中マッチとして登録されている時
            if (runningPos != null)
            {
                // 新しい統合マッチを自分と同じ位置に置く
                executor.Running_SetMatchPos(unit, runningPos);

                // 自分を走行中マッチリストから削除する
                executor.Running_RemoveMatch(this);
            }

            return unit;
        }

        #region タグ関連
        
        public MinimalMatch ToTagedTree()
        {
            var newList = new List<MinimalMatch>();
            var tag = "";
            if (this is CaptureMatch capture)
            {
                tag = capture.Tag;
            }
            this.BuildTagedTree(newList);
            var newOutput = new MinimalMatch(
                this.TextBegin, this.TextEnd,
                newList.ToArray(), tag, this);

            return newOutput;
        }
        public void BuildTagedTree(List<MinimalMatch> currentList)
        {
            foreach (var subMatch in SubMatches)
            {
                if (subMatch is CaptureMatch capture)
                {
                    var newList = new List<MinimalMatch>();

                    subMatch.BuildTagedTree(newList);
                    var newOutput = new MinimalMatch(
                        subMatch.TextBegin, subMatch.TextEnd, 
                        newList.ToArray(), capture.Tag, this);
                    currentList.Add(newOutput);
                }
                else
                {
                    subMatch.BuildTagedTree(currentList);
                }
            }
        }
        #endregion
    }

    #endregion

    public class MinimalMatch : IReadOnlyList<MinimalMatch>
    {
        public int Begin { get; private set; }
        public int End { get; private set; }
        public int Length
        {
            get { return End - Begin; }
        }

        public string Tag { get; private set; }

        public MinimalMatch[] SubMatches { get; private set; }

        public int Count
        {
            get { return SubMatches.Length; }
        }

        public string UniqID { get; private set; }
        public Matcher Generator { get; private set; }
        public MinimalMatch(int begin, int end, MinimalMatch[] subMatches, string tag, Match sorce)
        {
            Begin = begin;
            End = end;
            SubMatches = subMatches;
            Tag = tag;

            UniqID = sorce.UniqID;
            Generator = sorce.Generator;
        }

        //public MinimalMatch(int begin , int end, MinimalMatch[] subMatches, string tag)
        //{
        //    Begin = begin;
        //    End = end;
        //    SubMatches = subMatches;
        //    Tag = tag;
        //}

        public MinimalMatch this[int i]
        {
            get
            {
                return SubMatches[i];
            }
        }

        public IEnumerator<MinimalMatch> GetEnumerator()
        {
            foreach(var subMatch in SubMatches)
            {
                yield return subMatch;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var subMatch in SubMatches)
            {
                yield return subMatch;
            }
        }
        public string ToString(string text)
        {
            return text.Substring(Begin, Length);
        }
    }

    #region ロングマッチの親となり得るマッチ
    public abstract class LongOwnerMatch : Match
    {
        public LongOwnerMatch()
        { }

        private protected bool _hasActiveChild = false;
        public virtual bool HasActiveTail
        {
            get
            {
                return _hasActiveChild;
            }
            private protected set
            {
                _hasActiveChild = value;
            }
        }
        public abstract void DisableTail(Executor e);

        private protected void InnerDisable(Executor executor, Match inner)
        {
            // Leftの配下に追加可能なLongMatchがある時は追加不可にする
            if (inner is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(executor);
                }
            }
        }

        private protected void SetHasActiveTail(Match lastInner)
        {
            // 最後の内包要素が「追記可能な配下を持つ」時、
            // このマッチ自体も「追記可能な配下を持つ」として扱う
            if (lastInner is LongOwnerMatch longOwnerRight)
            {
                if (longOwnerRight.HasActiveTail)
                {
                    HasActiveTail = true;
                }
            }

        }
    }
    #endregion

    #region 出力用マッチ
    public class OutputMatch : Match
    {
        private int _textBeginIndex;
        private int _textEndIndex;
        private List<OutputMatch> _inners;
        private string _tag;

        public OutputMatch(int begin, int end)
        {
            _textBeginIndex = begin;
            _textEndIndex = end;
        }

        public void Add(OutputMatch subMatch)
        {
            if (_inners == null)
            {
                _inners = new List<OutputMatch>();
            }
            _inners.Add(subMatch);
        }

        public override IEnumerable<Match> SubMatches
        {
            get
            {
                if(_inners == null) { yield break; }
                foreach(var inner in _inners)
                {
                    yield return inner;
                }
            }
        }
        

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return _subMatches.Count == 0; }
        }
        public override int TextBegin
        {
            get { return _textBeginIndex; }
        }
        public override int TextEnd
        {
            get { return _textEndIndex; }
        }
        public override string ToString()
        {
            return "";
        }

        public string Tag
        {
            get { return _tag; }
            internal set
            {
                _tag = value;
            }
        }

    }
    #endregion

    #region １文字のマッチ結果
    /// <summary>
    /// １文字のマッチ結果
    /// </summary>
    public class CharMatch : Match
    {
        private int _textBeginIndex;

        /// <summary>
        /// 末端マッチャーから生成するコンストラクタ
        /// </summary>
        /// <param name="begin">マッチした位置</param>
        /// <param name="generator">このマッチを生成した末端マッチャー</param>
        public CharMatch(Executor executor, int begin, CharMatcher generator)
        {

            //Debug.WriteLine($"*{UniqID} Char {executor.Text[begin]}");

            _textBeginIndex = begin;

            Generator = generator;
            _subMatches = new List<Match>();
            Char = executor.Text[begin];
            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);
        }

        public override IEnumerable<Match> SubMatches
        {
            get { yield break; }
        }
        

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return true; }
        }
        public override int TextBegin
        {
            get { return _textBeginIndex; }
        }
        public override int TextEnd
        {
            get { return _textBeginIndex + 1; }
        }

        public char Char { get;  private set; }
        public override string ToString()
        {
            return Char.ToString();
        }

    }

    #endregion

    #region 長さゼロマッチ
    /// <summary>
    /// 長さゼロのマッチ結果
    /// </summary>
    public class ZeroLengthMatch : Match
    {
        private int _textBeginIndex;

        public ZeroLengthMatch(Executor executor, int begin, IZeroLength generator)
        {
            //Debug.WriteLine($"*{UniqID} Zero");

            _textBeginIndex = begin;
            Generator = (Matcher)generator;

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);
        }
        public override IEnumerable<Match> SubMatches
        {
            get { yield break; }
        }
        
        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return true; }
        }


        public override int TextBegin
        {
            get { return _textBeginIndex; }
        }
        public override int TextEnd
        {
            get { return _textBeginIndex; }
        }
        public override string ToString()
        {
            return "";
        }

    }
    #endregion

    #region 開始マッチ
    public class BeginMatch : ZeroLengthMatch
    {
        public BeginMatch(Executor executor, IZeroLength generator)
            : base(executor, 0, generator)
        {
            //Debug.WriteLine($"*{UniqID} Begin");
        }

        public override string ToString()
        {
            return "(BEGIN)";
        }
    }
    #endregion

    #region 終了マッチ
    public class EndMatch : ZeroLengthMatch
    {
        public EndMatch(Executor executor, int begin, IZeroLength generator)
            : base(executor, begin, generator)
        {
            //Debug.WriteLine($"*{UniqID} End");
        }

        public override string ToString()
        {
            return "(END)";
        }
    }
    #endregion

    #region ペアマッチ
    /// <summary>
    /// ペアマッチャーから発生するマッチ
    /// </summary>
    public class PairMatch : LongOwnerMatch
    {
        private LeftMatch _leftMatch;
        private Match _rightMatch;

        public PairMatch(
            Executor executor, LeftMatch leftInner, RightMatch rightInner, 
            PairMatcher generator)
        {
            //Debug.WriteLine($"*{UniqID} Pair ({leftInner.UniqID} {rightInner.UniqID})");

            Generator = generator;
            _leftMatch = leftInner;
            _rightMatch = rightInner;

            // Leftの配下に追加可能なLongMatchがある時は追加不可にする
            InnerDisable(executor, _leftMatch);

            // Rightの配下に追加可能なLongMatchがある時は
            // Pairマッチ自体が追加可能となる
            SetHasActiveTail(_rightMatch);

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            // 上がってきたマッチの親カウントを増やす
            executor.ParentsCountPlus(leftInner);
            executor.ParentsCountPlus(rightInner);

            //Debug.WriteLine("**PairMatch Created**");
            //executor.ViewMatchTree(this);
            //Debug.WriteLine("****");
        }
        public override IEnumerable<Match> SubMatches
        {
            get
            {
                yield return _leftMatch;
                yield return _rightMatch;
            }
        }
        
        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }


        public override int TextBegin
        {
            get { return _leftMatch.TextBegin; }
        }
        public override int TextEnd
        {
            get { return _rightMatch.TextEnd; }
        }
        public override string ToString()
        {
            return _leftMatch.ToString() + _rightMatch.ToString();
        }
        public override Match Unit(Executor executor, Match other)
        {
            if (other is PairMatch otherPair)
            {
                var otherRight = otherPair._rightMatch;
                // _leftMatch同士が共通かつ、右同士が重複範囲なら、_rightMatch同士を統合する
                if ((otherPair._leftMatch == _leftMatch) &&
                    ((otherPair._rightMatch.TextBegin == _rightMatch.TextBegin) &&
                        (otherPair._rightMatch.TextEnd == _rightMatch.TextEnd)))
                {
                    _rightMatch = _rightMatch.Unit(executor, otherPair._rightMatch);
                    return this;
                }

            }

            return base.Unit(executor, other);
        }

        public Match Left
        {
            get { return _leftMatch; }
        }
        public Match Right
        {
            get { return _rightMatch; }
        }

        #region Longマッチの末尾処理

        public override void DisableTail(Executor e)
        {
            if(HasActiveTail == false )
            {
                return;
            }

            // Rightの配下に追加可能なLongMatchがある時は追加不可にする
            if (_rightMatch is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(e);
                }
            }

            HasActiveTail = false;
        }
        #endregion

    }

    /// <summary>
    /// ペアマッチャーの左に上がっていくマッチ
    /// </summary>
    public class LeftMatch : LongOwnerMatch
    {
        private Match _inner;

        public LeftMatch(Executor executor, Match inner, LeftMatcher generator)
        {
            //Debug.WriteLine($"*{UniqID} Left ({inner.UniqID})");

            Generator = generator;
            _inner = inner;

            // innerの配下に追加可能なLongMatchがある時は
            // Tagマッチ自体が追加可能となる
            SetHasActiveTail(inner);

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            // 内包要素の親カウントを増やす
            executor.ParentsCountPlus(inner);
        }
        public Match Inner
        {
            get { return _inner; }
        }

        public override IEnumerable<Match> SubMatches
        {
            get
            {
                yield return _inner;
            }
        }
        
        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.NotConnect; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }


        public override int TextBegin
        {
            get { return _inner.TextBegin; }
        }
        public override int TextEnd
        {
            get { return _inner.TextEnd; }
        }
        public override string ToString()
        {
            return _inner.ToString();
        }
        /// <summary>
        /// このマッチと統合する別のマッチを指定して統合マッチに変化させる。
        /// 停止マッチリストに在る自分の参照を統合マッチの参照に上書きする。
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="other">統合する別のマッチ</param>
        /// <returns>統合する要素が加わった後の自分</returns>
        public override Match Unit(Executor executor, Match other)
        {

            if (other is LeftMatch otherLeft)
            {
                _inner = _inner.Unit(executor, otherLeft.Inner);
            }
            else
            {
                _inner = _inner.Unit(executor, other);
            }
            return this;
        }

        #region Longマッチの末尾処理
        public override void DisableTail(Executor e)
        {
            if (HasActiveTail == false)
            {
                return;
            }

            // innerの配下に追加可能なLongMatchがある時は追加不可にする
            if (_inner is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(e);
                }
            }
            HasActiveTail = false;
        }
        #endregion

    }

    /// <summary>
    /// ペアマッチャーの右に上がっていくマッチ
    /// </summary>
    public class RightMatch : LongOwnerMatch
    {
        private Match _inner;

        public RightMatch(Executor executor, Match inner, RightMatcher generator)
        {
            //Debug.WriteLine($"*{UniqID} Right ({inner.UniqID})");

            Generator = generator;
            _inner = inner;

            // innerの配下に追加可能なLongMatchがある時は
            // Tagマッチ自体が追加可能となる
            SetHasActiveTail(inner);

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            // 内包要素の親カウントを増やす
            executor.ParentsCountPlus(inner);
        }

        public Match Inner
        {
            get { return _inner; }
        }

        public override IEnumerable<Match> SubMatches
        {
            get
            {
                yield return _inner;
            }
        }

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.NotConnect; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }


        public override int TextBegin
        {
            get { return _inner.TextBegin; }
        }
        public override int TextEnd
        {
            get { return _inner.TextEnd; }
        }
        public override string ToString()
        {
            return _inner.ToString();
        }
        /// <summary>
        /// このマッチと統合する別のマッチを指定して統合マッチに変化させる。
        /// 停止マッチリストに在る自分の参照を統合マッチの参照に上書きする。
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="other">統合する別のマッチ</param>
        /// <returns>統合する要素が加わった後の自分</returns>
        public override Match Unit(Executor executor, Match other)
        {

            if (other is RightMatch otherRight)
            {
                _inner = _inner.Unit(executor, otherRight.Inner);
            }
            else
            {
                _inner = _inner.Unit(executor, other);
            }
            return this;
        }

        #region Longマッチの末尾処理
        public override void DisableTail(Executor e)
        {
            if (HasActiveTail == false)
            {
                return;
            }

            // innerの配下に追加可能なLongMatchがある時は追加不可にする
            if (_inner is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(e);
                }
            }
            HasActiveTail = false;
        }
        #endregion

    }
    #endregion


    #region LongHeadMatch
    public class LongHeadMatch : LongOwnerMatch
    {
        private Match _inner;
        public LongHeadMatch(Executor executor, Match inner, LongHeadMatcher generator)
        {
            Generator = generator;
            _inner = inner;
            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            // 内包要素の親カウントを加算する
            executor.ParentsCountPlus(inner);
        }
        public Match Inner
        {
            get { return _inner; }
        }

        public override IEnumerable<Match> SubMatches
        {
            get
            {
                yield return _inner;
            }
        }

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.NotConnect; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }


        public override int TextBegin
        {
            get { return _inner.TextBegin; }
        }
        public override int TextEnd
        {
            get { return _inner.TextEnd; }
        }
        public override string ToString()
        {
            return _inner.ToString();
        }
        /// <summary>
        /// このマッチと統合する別のマッチを指定して統合マッチに変化させる。
        /// 停止マッチリストに在る自分の参照を統合マッチの参照に上書きする。
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="other">統合する別のマッチ</param>
        /// <returns>統合する要素が加わった後の自分</returns>
        public override Match Unit(Executor executor, Match other)
        {

            if (other is LeftMatch otherLeft)
            {
                _inner = _inner.Unit(executor, otherLeft.Inner);
            }
            else
            {
                _inner = _inner.Unit(executor, other);
            }
            return this;
        }

        #region Longマッチの末尾処理
        public override void DisableTail(Executor e)
        {
            if (HasActiveTail == false)
            {
                return;
            }

            // innerの配下に追加可能なLongMatchがある時は追加不可にする
            if (_inner is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(e);
                }
            }
            HasActiveTail = false;
        }
        #endregion
    }
    #endregion


    #region ロングマッチ
    public class LongMatch : LongOwnerMatch
    {
        private List<Match> _inners;

        public JumpMatch JumpSorce { get; private set; }

        public bool IsActive { get; private set; }

        public LongMatch(Executor executor, Match inner, LongMatcher generator)
        {
            //Debug.WriteLine($"*{UniqID} Long ({inner.UniqID})");

            if (inner is LongOwnerMatch longOwner)
            {
                if(longOwner.HasActiveTail)
                {
                    _hasActiveChild = true;
                }
            }

            Generator = generator;
            _inners = new List<Match>();
            _inners.Add(inner);

            JumpSorce = new JumpMatch(executor, this);
            // 跳躍マッチの現在位置をこのマッチを発生させたマッチャーに設定する。
            executor.Staying_SetMatchPos(JumpSorce, generator);


            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            // 内包要素の親カウントを増やす
            executor.ParentsCountPlus(inner);
        }

        public void Add(Executor executor, Match inner)
        {
            // 末尾のinnerの配下に追加可能なLongMatchがある時は追加不可にする
            InnerDisable(executor, _inners[_inners.Count - 1]);

            _inners.Add(inner);
            // 内包要素の親カウントを増やす
            executor.ParentsCountPlus(inner);

            // 追加するinnerの配下に追加可能なLongMatchがある時は
            // 自分の HasActiveTail に反映する
            if (inner is LongOwnerMatch newLongOwner)
            {
                if (newLongOwner.HasActiveTail)
                {
                    _hasActiveChild = true;
                }
            }
        }

        /// <summary>
        /// サブマッチを列挙する
        /// </summary>
        public override IEnumerable<Match> SubMatches
        {
            get
            {
                return _inners;
            }
        }

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }
        public override int TextBegin
        {
            get { return _inners[0].TextBegin; }
        }
        public override int TextEnd
        {
            get { return _inners[_inners.Count - 1].TextEnd; }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var inner in _inners)
            {
                sb.Append(inner.ToString());
            }

            return sb.ToString();
        }

        // private bool _isSelfActive = true;

        public override bool HasActiveTail
        {
            get
            {
                bool isSelfActive = (JumpSorce != null);
                return _hasActiveChild || isSelfActive;
            }
            private protected set
            {
                _hasActiveChild = value;
            }
        }

        public override void DisableTail(Executor e)
        {
            if(_hasActiveChild)
            {
                // 末尾のinnerの配下に追加可能なLongMatchがある時は追加不可にする
                if (_inners[_inners.Count - 1] is LongOwnerMatch longOwner)
                {
                    if (longOwner.HasActiveTail)
                    {
                        longOwner.DisableTail(e);
                    }
                }
                _hasActiveChild = false;
            }

            // 自分に対する処理
            if (JumpSorce != null)
            {
                JumpSorce.UnWrap(e);
                JumpSorce = null;
            }
        }

        public override void UnWrap(Executor executor)
        {
            // 自分に対する処理
            if (JumpSorce != null)
            {
                JumpSorce.UnWrap(executor);
                JumpSorce = null;
            }
            base.UnWrap(executor);
        }

    }
    #endregion

    #region 跳躍マッチ
    public class JumpMatch : Match
    {
        public LongMatch JumpTarget { get; private set; }

        public JumpMatch(Executor executor, LongMatch jumpTarget)
        {
            //Debug.WriteLine($"*{UniqID} Jump (Target={jumpTarget.UniqID})");

            JumpTarget = jumpTarget;

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);
        }

        public override bool IsTerminal
        {
            get { return false; }
        }

        public override int TextBegin
        {
            get { return JumpTarget.TextBegin; }
        }

        public override int TextEnd
        {
            get { return JumpTarget.TextBegin; }
        }

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.NotConnect; }
        }

    }
    #endregion

    

    #region ループマッチ
    /// <summary>
    /// ループマッチから完成品として上がっていくマッチ
    /// </summary>
    public class LoopMatch : LongOwnerMatch
    {
        private List<Match> _inners;
        public WaitingMatch PrevWaitingMatch
        { get; private set; }

        /// <summary>
        /// WaitingMatchのコピーとして作成するコンストラクタ
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="inners">元のWaitingMatchの内包要素</param>
        /// <param name="generator">このマッチを生成したLoopマッチャー</param>
        public LoopMatch(Executor executor, IEnumerable<Match> inners, 
            LoopMatcher generator, WaitingMatch prev)
        {
            var sb = new StringBuilder();
            foreach(var inner in inners)
            {
                sb.Append($"{inner.UniqID} ");
            }
            //Debug.WriteLine($"*{UniqID} Loop ({sb.ToString()})");

            PrevWaitingMatch = prev;
            Generator = generator;
            _inners = new List<Match>(inners);

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            foreach (var inner in _inners)
            {
                // 内包要素の親カウントを加算する
                executor.ParentsCountPlus(inner);
            }
        }

        /// <summary>
        /// サブマッチを列挙する
        /// </summary>
        public override IEnumerable<Match> SubMatches
        {
            get
            {
                return _inners;
            }
        }

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }
        public override int TextBegin
        {
            get { return _inners[0].TextBegin; }
        }
        public override int TextEnd
        {
            get { return _inners[_inners.Count - 1].TextEnd; }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var inner in _inners)
            {
                sb.Append(inner.ToString());
            }

            return sb.ToString();
        }
        public override Match Unit(Executor executor, Match other)
        {
            if (other is LoopMatch loop)
            {
                // 最終要素以前が一致する時
                if (PrevWaitingMatch == loop.PrevWaitingMatch)
                {
                    // 最終要素だけを統合する
                    var unit = _inners[_inners.Count - 1].Unit
                        (executor, loop._inners[loop._inners.Count - 1]);
                    _inners[_inners.Count - 1] = unit;
                    return this;
                }
            }

            return base.Unit(executor, other);
        }

        #region Longマッチの末尾処理
        public override void DisableTail(Executor e)
        {
            if (HasActiveTail == false)
            {
                return;
            }

            // 末尾のinnerの配下に追加可能なLongMatchがある時は追加不可にする
            if (_inners[_inners.Count - 1] is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(e);
                }
            }
            HasActiveTail = false;
        }
        #endregion

    }
    #endregion

    #region ループマッチャーの上で待機するマッチ
    /// <summary>
    /// ループマッチャーの上で待機するマッチ
    /// </summary>
    public class WaitingMatch : LongOwnerMatch
    {
        private List<Match> _inners;

        public WaitingMatch PrevWaitingMatch
        { get; private set; }
        /// <summary>
        /// 先頭の内包要素を指定するコンストラクタ
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="firstMatch">先頭の内包要素</param>
        /// <param name="generator">このマッチを生成したLoopマッチャー</param>
        public WaitingMatch(Executor executor, Match firstMatch, LoopMatcher generator)
        {
            //Debug.WriteLine($"*{UniqID} Waiting (First={firstMatch.UniqID})");

            Generator = generator;
            _inners = new List<Match>();
            _inners.Add(firstMatch);

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            // 先頭要素の親カウントを加算する
            executor.ParentsCountPlus(firstMatch);
        }

        /// <summary>
        /// 内部使用コンストラクタ(CreateAppendedInstance用)
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="org">元のインスタンス</param>
        /// <param name="appendInner">追加要素</param>
        private WaitingMatch(Executor executor, WaitingMatch org, Match appendInner)
        {
            PrevWaitingMatch = org;
            Generator = org.Generator;
            _inners = new List<Match>(org._inners);

            // 末尾のinnerの配下に追加可能なLongMatchがある時は追加不可にする
            if (_inners[_inners.Count - 1] is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(executor);
                }
            }

            _inners.Add(appendInner);

            // 追加するinnerの配下に追加可能なLongMatchがある時は
            // 自分の HasActiveTail に反映する
            if (appendInner is LongOwnerMatch newLongOwner)
            {
                if (newLongOwner.HasActiveTail)
                {
                    HasActiveTail = true;
                }
            }

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            foreach (var inner in _inners)
            {
                // 内包要素の親カウントを加算する
                executor.ParentsCountPlus(inner);
            }
        }

        /// <summary>
        /// 内包要素を追加された新しいインスタンスを作成する
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="appendInner">追加要素</param>
        /// <returns>内包要素を追加された新しいインスタンス</returns>
        public WaitingMatch CreateAppendedInstance(Executor executor, Match appendInner)
        {
            return new WaitingMatch(executor, this, appendInner);
        }

        /// <summary>
        /// サブマッチを列挙する
        /// </summary>
        public override IEnumerable<Match> SubMatches
        {
            get
            {
                return _inners;
            }
        }

        /// <summary>
        /// サブマッチの数を返す
        /// </summary>
        public int InnersCount
        {
            get { return _inners.Count; }
        }
        
        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.NotConnect; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }
        public override int TextBegin
        {
            get { return _inners[0].TextBegin; }
        }
        public override int TextEnd
        {
            get { return _inners[_inners.Count - 1].TextEnd; }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var inner in _inners)
            {
                sb.Append(inner.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// この待機マッチと同じ内容のループマッチを作る
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <returns></returns>
        public LoopMatch Copy(Executor executor)
        {
            return new LoopMatch(executor, _inners, (LoopMatcher)Generator, PrevWaitingMatch);
        }

        #region Longマッチの末尾処理
        public override void DisableTail(Executor e)
        {
            if (HasActiveTail == false)
            {
                return;
            }

            // 末尾のinnerの配下に追加可能なLongMatchがある時は追加不可にする
            if (_inners[_inners.Count - 1] is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(e);
                }
            }
            HasActiveTail = false;
        }
        #endregion

    }
    #endregion

    #region Captureマッチ
    /// <summary>
    /// Eitherマッチがタグを持つ時、上がってきたinnerMatchをラップする為のマッチ
    /// </summary>
    public class CaptureMatch : LongOwnerMatch
    {
        private Match _inner;
        public string Tag { get; private set; }

        public CaptureMatch(Executor executor, Match inner, Matcher generator, string tag)
        {
            //Debug.WriteLine($"*{UniqID} Capture ({inner.UniqID})");
            
            _inner = inner;
            Generator = generator;
            Tag = tag;

            // innerの配下に追加可能なLongMatchがある時は
            // Tagマッチ自体が追加可能となる
            SetHasActiveTail(inner);

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            // 内包要素の親カウントを増やす
            executor.ParentsCountPlus(inner);
        }

        public override IEnumerable<Match> SubMatches
        {
            get { yield return _inner; }
        }

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }
        public override int TextBegin
        {
            get { return _inner.TextBegin; }
        }
        public override int TextEnd
        {
            get { return _inner.TextEnd; }
        }
        public override string ToString()
        {
            return _inner.ToString();
        }

        #region Longマッチの末尾処理
        public override void DisableTail(Executor e)
        {
            if (HasActiveTail == false)
            {
                return;
            }

            // innerの配下に追加可能なLongMatchがある時は追加不可にする
            if (_inner is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(e);
                }
            }
            HasActiveTail = false;
        }
        #endregion
    }
    #endregion

    #region Rootマッチ
    /// <summary>
    /// ルートマッチャーで発生するマッチ。
    /// </summary>
    public class RootMatch : LongOwnerMatch
    {
        private Match _inner;

        public RootMatch(Executor executor, Match inner, RootMatcher generator)
        {
            //Debug.WriteLine($"*{UniqID} Root ({inner.UniqID})");

            _inner = inner;
            Generator = generator;

            // 自分自身を実行器に登録する。
            executor.RegisterMatch(this);

            // 内包要素の親カウントを増やす
            executor.ParentsCountPlus(inner);
        }

        public override IEnumerable<Match> SubMatches
        {
            get { yield return _inner; }
        }

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }
        public override bool IsTerminal
        {
            get { return false; }
        }
        public override int TextBegin
        {
            get { return _inner.TextBegin; }
        }
        public override int TextEnd
        {
            get { return _inner.TextEnd; }
        }
        public override string ToString()
        {
            return _inner.ToString();
        }

        #region Longマッチの末尾処理

        public override void DisableTail(Executor e)
        {
            if (HasActiveTail == false)
            {
                return;
            }

            // innerの配下に追加可能なLongMatchがある時は追加不可にする
            if (_inner is LongOwnerMatch longOwner)
            {
                if (longOwner.HasActiveTail)
                {
                    longOwner.DisableTail(e);
                }
            }
            HasActiveTail = false;
        }
        #endregion
    }
    #endregion

    #region 統合マッチ
    /// <summary>
    /// 開始インデックス・終了インデックス・位置が同じマッチを統合させて一つとして扱うマッチ
    /// </summary>
    public class UnitMatch : LongOwnerMatch
    {
        private List<Match> _inners;

        public int InnerCount
        {
            get { return _inners.Count; }
        }
        public UnitMatch(Match inner)
        {
            _inners = new List<Match>(2);
            _inners.Add(inner);
        }

        public UnitMatch(Match innerX, Match innerY)
        {
            if (innerX.TextBegin != innerY.TextBegin) { throw new ArgumentOutOfRangeException(); }
            if (innerX.TextEnd != innerY.TextEnd) { throw new ArgumentOutOfRangeException(); }

            Generator = innerX.Generator;

            _inners = new List<Match>(2);
            _inners.Add(innerX);
            _inners.Add(innerY);

            _hasActiveChild = false;
            foreach (var inner in _inners)
            {
                // innerの配下に追加可能なLongMatchがある時は
                // Tagマッチ自体が追加可能となる
                if(inner is LongOwnerMatch longOwner)
                {
                    if(longOwner.HasActiveTail)
                    {
                        _hasActiveChild = true;
                    }
                }
            }
        }
        public void AddInner(Match newInner)
        {
            if (_inners[0].TextBegin != newInner.TextBegin) { throw new ArgumentOutOfRangeException(); }
            if (_inners[0].TextEnd != newInner.TextEnd) { throw new ArgumentOutOfRangeException(); }
            _inners.Add(newInner);

            if(_hasActiveChild == false)
            {
                if (newInner is LongOwnerMatch longOwner)
                {
                    if (longOwner.HasActiveTail)
                    {
                        _hasActiveChild = true;
                    }
                }
            }
        }

        public override IEnumerable<Match> SubMatches
        {
            get { return _inners; }
        }

        public override bool IsTerminal
        {
            get { return false; }
        }

        public override int TextBegin
        {
            get { return _inners[0].TextBegin; }
        }

        public override int TextEnd
        {
            get { return _inners[0].TextEnd; }
        }


        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }

        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }

        /// <summary>
        /// このマッチに統合する別のマッチを指定してinnersに追加する。
        /// </summary>
        /// <param name="executor">実行器</param>
        /// <param name="other">統合する別のマッチ</param>
        public override Match Unit(Executor executor, Match other)
        {
            for (var i = 0; i < _inners.Count; i++)
            {
                var inner = _inners[i];
                if ((inner is PairMatch pairThis) && (other is PairMatch pairOther))
                {
                    _inners[i] = pairThis.Unit(executor, pairOther);
                    return this;
                }
                if ((inner is LoopMatch loopThis) && (other is LoopMatch loopOther))
                {
                    _inners[i] = loopThis.Unit(executor, loopOther);
                    return this;
                }
            }

            _inners.Add(other);
            return this;
        }

        /// <summary>
        /// 複数の内包要素を持つ場合は自分を返し、内包要素が一つの時は内包要素を返す。
        /// </summary>
        /// <returns></returns>
        public Match ToSingleMatch()
        {
            if (_inners.Count == 0)
            {
                return _inners[0];
            }
            else
            {
                return this;
            }
        }

        #region Longマッチの末尾処理
        public override void DisableTail(Executor e)
        {

            if (HasActiveTail == false)
            {
                return;
            }

            // 子要素に対する処理
            foreach (var inner in _inners)
            {
                // innerの配下に追加可能なLongMatchがある時は追加不可にする
                if (inner is LongOwnerMatch longOwner)
                {
                    if (longOwner.HasActiveTail)
                    {
                        longOwner.DisableTail(e);
                    }
                }
            }
            HasActiveTail = false;
        }
        #endregion
    }
    #endregion

    #region インデント・デデントマッチの基底型
    public class IndentDedentMatchBase : Match
    {
        private int _textBeginIndex;
        private int _textEndIndex;

        public IndentDedentMatchBase(Executor executor, int begin, int end, Matcher generator)
        {
            _textBeginIndex = begin;
            _textEndIndex = end;
            Generator = generator;
        }

        public override IEnumerable<Match> SubMatches
        {
            get { yield break; }
        }

        public override bool IsTerminal
        {
            get { return true; }
        }

        public override int TextBegin
        {
            get { return _textBeginIndex; }
        }

        public override int TextEnd
        {
            get { return _textEndIndex; }
        }

        public override ConnectionStatus LeftConnection
        {
            get { return ConnectionStatus.Connected; }
        }

        public override ConnectionStatus RightConnection
        {
            get { return ConnectionStatus.Connected; }
        }

        
    }
    #endregion

    #region インデントマッチ
    /// <summary>
    /// インデントを表現するマッチ
    /// </summary>
    public class IndentMatch : IndentDedentMatchBase
    {

        public IndentMatch(Executor executor, int begin, int end, BuiltInMatcher.IndentMatcher generator)
            : base(executor, begin, end, generator)
        { }
    }
    #endregion

    #region デデントマッチ
    /// <summary>
    /// デデントを表現するマッチ
    /// </summary>
    public class DedentMatch : IndentDedentMatchBase
    {
        public DedentMatch(Executor executor, int begin, int end, BuiltInMatcher.DedentMatcher generator)
        : base(executor, begin, end, generator)
        { }
    }
    #endregion

    #region エラーデデントマッチ
    /// <summary>
    /// エラーデデントを表現するマッチ
    /// </summary>
    public class ErrorDedentMatch : IndentDedentMatchBase
    {
        public ErrorDedentMatch(Executor executor, int begin, int end, BuiltInMatcher.ErrorDedentMatcher generator)
        : base(executor, begin, end, generator)
        { }
    }
    #endregion

    
    #region MatchList

    /// <summary>
    /// 子要素となるMatchを列挙できるリスト
    /// </summary>
    /// <remarks>
    /// Match[]にToStringを拡張メソッドとして設定しても、
    /// object.ToStringが優先されてしまうので、
    /// このような新造クラスが必要となった。
    /// </remarks>
    public class MatchList : IEnumerable<Match>
    {
        private Func<IEnumerable<Match>> func;

        /// <summary>
        /// 列挙関数を指定するコンストラクタ
        /// </summary>
        /// <param name="func">子要素を列挙する関数</param>
        public MatchList(Func<IEnumerable<Match>> func)
        {
            this.func = func;
        }

        /// <summary>
        /// リストそのものを指定するコンストラクタ
        /// </summary>
        /// <param name="items"></param>
        public MatchList(IEnumerable<Match> items)
        {
            this.func = () =>
            {
                return items;
            };
        }

        /// <summary>
        /// フィルタリングした結果のリストを返す。
        /// </summary>
        /// <param name="filterFunc">フィルタリング用関数</param>
        /// <returns></returns>
        public MatchList Filter(Func<Match, bool> filterFunc)
        {
            IEnumerable<Match> func()
            {
                return FilterBody(filterFunc);
            }

            return new MatchList(func);
        }

        /// <summary>
        /// Filter()関数の本体部
        /// </summary>
        /// <param name="filterFunc">フィルタリング用関数</param>
        /// <returns></returns>
        private IEnumerable<Match> FilterBody(Func<Match, bool> filterFunc)
        {
            foreach (var temp in this.func())
            {
                if (filterFunc(temp))
                {
                    yield return temp;
                }
            }
        }

        /// <summary>
        /// 子孫全ての世代を直列に列挙するリストを返す。
        /// </summary>
        /// <returns></returns>
        public MatchList Serialize()
        {
            return new MatchList(Serialize_Body(this.func()));
        }

        /// <summary>
        /// Serialize()関数の本体
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private IEnumerable<Match> Serialize_Body(IEnumerable<Match> list)
        {
            foreach (var item in list)
            {
                yield return item;

                foreach (var subItem in Serialize_Body(item))
                {
                    yield return subItem;
                }

            }
        }

        public IEnumerator<Match> GetEnumerator()
        {
            foreach (var item in func())
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var item in func())
            {
                yield return item;
            }
        }





        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in func())
            {
                sb.Append(item.ToString());
            }

            return sb.ToString();
        }
    }
    #endregion MatchList
}
