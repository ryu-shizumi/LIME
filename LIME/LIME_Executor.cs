using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using System.Diagnostics;

namespace LIME
{
    #region 実行器
    /// <summary>
    /// マッチング作業を受け付ける「実行器」。
    /// </summary>
    /// <remarks>
    /// 列挙子は、入力文字列を１文字ずつ消費して、ルートに達する事ができたかを返す。
    /// 
    /// </remarks>
    public class Executor : IEnumerable<Match>
    {
        /// <summary>
        /// ルートマッチャー
        /// </summary>
        private RootMatcher _root;

        /// <summary>
        /// 入力文字を食わせる末端マッチャーのリスト
        /// </summary>
        private HashSet<IReceiveChar> _terminalList;

        /// <summary>
        /// 長さゼロでマッチする末端マッチャーのリスト
        /// </summary>
        private HashSet<IZeroLength> _zeroLengthList;

        #region インデント検出関連

        /// <summary>
        /// インデントマッチャーのリスト
        /// </summary>
        private HashSet<BuiltInMatcher.IndentMatcher> _indentMatcherList;
        /// <summary>
        /// デデントマッチャーのリスト
        /// </summary>
        private HashSet<BuiltInMatcher.DedentMatcher> _dedentMatcherList;
        /// <summary>
        /// エラーデデントマッチャーのリスト
        /// </summary>
        private HashSet<BuiltInMatcher.ErrorDedentMatcher> _errorDedentMatcherList;

        #endregion

        /// <summary>
        /// ブランクのリスト
        /// </summary>
        private SortedList<int, TextRange> _blankList;

        /// <summary>
        /// ブランク(空白・改行)の文字列範囲を格納するリスト。
        /// マッチャー同士が連結する際、ブランクを挟み込む事ができる。
        /// 範囲の隣り合うブランクは１個のブランクとして扱われる。
        /// </summary>
        public SortedList<int, TextRange> BlankList
        {
            get{ return _blankList; }
        }

        /// <summary>
        /// 先頭マッチャーのリスト
        /// </summary>
        private HashSet<BuiltInMatcher.BeginMatcher> _beginMatcherList;

        /// <summary>
        /// 末尾マッチャーのリスト
        /// </summary>
        private HashSet<BuiltInMatcher.EndMatcher> _endMatcherList;

        /// <summary>
        /// 単語区切りマッチャーのリスト
        /// </summary>
        private HashSet<BuiltInMatcher.WordBreakMatcher> _wordBreakMatcherList;

        /// <summary>
        /// マッチング実行時におけるマッチャーの親子関係(子から親)を保持するハッシュマップ
        /// </summary>
        private Dictionary<Matcher, HashSet<IHasInner>> _childToParent;

        /// <summary>
        /// マッチング実行時におけるマッチャーの親子関係(親から子)を保持するハッシュマップ
        /// </summary>
        private Dictionary<IHasInner, HashSet<Matcher>> _parentToChild;

        public IEnumerable<IHasInner> GetParents(Matcher matcher)
        {
            return _childToParent[matcher];
        }
        public IEnumerable<Matcher> GetChildren(IHasInner parent)
        {
            return _parentToChild[parent];
        }


        private class MatchComp : IComparer<Match>
        {
            public int Compare(Match x, Match y)
            {
                // x < y なら負を返す
                // x == y ならゼロを返す
                // x > y なら正を返す

                var lengthDiff = x.TextLength - y.TextLength;

                if (lengthDiff != 0)
                {
                    return lengthDiff;
                }

                var startDiff = x.TextBegin - y.TextBegin;
                if (startDiff != 0)
                {
                    return startDiff;
                }

                return ((object)x).GetHashCode() - ((object)y).GetHashCode();
            }
        }

        public enum MatchState
        {
            Running,
            Staying
        }

        /// <summary>
        /// 走行中マッチ位置管理リスト
        /// (ループ・連結マッチャー上で停止しているマッチ群のそれぞれの位置)
        /// </summary>
        /// <remarks>
        /// キー：マッチ(長さでソートされる)
        /// 値　：マッチが停止しているループ・連結マッチャー
        /// </remarks>
        private SortedDictionary<Match, Matcher> _running_MatchToPos
            = new SortedDictionary<Match, Matcher>(new MatchComp());

        private Dictionary<Matcher, SortedDictionary<int, HashSet<Match>>> _running_PosToMatch
            = new Dictionary<Matcher, SortedDictionary<int, HashSet<Match>>>();

        /// <summary>
        /// PairマッチャーやLoopマッチャー上で停止したマッチのリスト
        /// </summary>
        /// <remarks>
        /// マッチャーを指定してそこで停止しているマッチを得る。
        /// マッチャーを指定して同じ長さのマッチを得る。
        /// </remarks>
        private Dictionary<Matcher, SortedDictionary<int, HashSet<Match>>> _staying_PosToMatch
            = new Dictionary<Matcher, SortedDictionary<int, HashSet<Match>>>();
        /// <summary>
        /// 停止リストの逆引き辞書
        /// </summary>
        private Dictionary<Match, Matcher> _staying_MatchToPos
            = new Dictionary<Match, Matcher>();

        /// <summary>
        /// 走行中マッチの現在位置を設定する
        /// </summary>
        /// <param name="match">マッチ</param>
        /// <param name="matcher">マッチの現在位置</param>
        public void Running_SetMatchPos(Match match, Matcher matcher)
        {
            // _running_MatchToPos[match] = matcher;


            // マッチャーが未知なら新要素を追加する
            if (_running_PosToMatch.ContainsKey(matcher) == false)
            {
                _running_PosToMatch.Add(matcher, new SortedDictionary<int, HashSet<Match>>());
            }
            var stateDict = _running_PosToMatch[matcher];
            var length = match.TextLength;
            // 同じ長さのマッチがまだ無い時
            if (stateDict.ContainsKey(length) == false)
            {
                stateDict.Add(length, new HashSet<Match>());
            }
            stateDict[length].Add(match);
            // 逆引き辞書にもレコードを追加する
            _running_MatchToPos.Add(match, matcher);
        }
        public Match Running_FindMatch(Matcher matcher, int textBegin, int textEnd)
        {
            int length = textEnd - textBegin;

            if (_running_PosToMatch.ContainsKey(matcher) == false)
            {
                return null;
            }
            if (_running_PosToMatch[matcher].ContainsKey(length) == false)
            {
                return null;
            }
            var list = _running_PosToMatch[matcher][length];

            foreach (var match in list)
            {
                if (match.TextBegin == textBegin)
                {
                    return match;
                }
            }
            return null;
        }
        public Match[] Running_PosToMatch(Matcher matcher)
        {
            var result = new List<Match>();
            if(_running_PosToMatch.ContainsKey(matcher))
            {
                var temp = _running_PosToMatch[matcher];
                foreach (var value in temp.Values)
                {
                    result.AddRange(value);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 指定したマッチャー上に位置しているマッチを全て列挙する。
        /// </summary>
        /// <param name="matcher">マッチャー</param>
        /// <returns></returns>
        public Match[] Staying_PosToMatch(Matcher matcher)
        {
            if (_staying_PosToMatch.ContainsKey(matcher) == false)
            {
                return new Match[] { };
            }
            var values = _staying_PosToMatch[matcher].Values;

            // 返信する配列のサイズを計算する
            var count = 0;
            foreach (var list in values)
            {
                count += list.Count;
            }
            // 計算したサイズで返信配列を作る
            var result = new Match[count];
            count = 0;
            foreach (var list in values)
            {
                // 返信配列にリストをコピーしていく
                list.CopyTo(result, count);
                count += list.Count;
            }
            return result;
        }

        /// <summary>
        /// 任意のマッチャーに居るマッチを条件を絞って探す
        /// </summary>
        /// <param name="matcher"></param>
        /// <param name="textBegin"></param>
        /// <param name="textEnd"></param>
        /// <returns></returns>
        public Match Staying_FindMatch(Matcher matcher, int textBegin, int textEnd)
        {
            int length = textEnd - textBegin;

            if (_staying_PosToMatch.ContainsKey(matcher) == false)
            {
                return null;
            }
            if (_staying_PosToMatch[matcher].ContainsKey(length) == false)
            {
                return null;
            }
            var list = _staying_PosToMatch[matcher][length];

            foreach (var match in list)
            {
                if (match.TextBegin == textBegin)
                {
                    return match;
                }
            }
            return null;
        }

        /// <summary>
        /// マッチの現在位置を設定する
        /// </summary>
        /// <param name="match">マッチ</param>
        /// <param name="matcher">マッチの現在位置</param>
        public void Staying_SetMatchPos(Match match, Matcher matcher)
        {
            // マッチャーが未知なら新要素を追加する
            if (_staying_PosToMatch.ContainsKey(matcher) == false)
            {
                _staying_PosToMatch.Add(matcher, new SortedDictionary<int, HashSet<Match>>());
            }
            var stateDict = _staying_PosToMatch[matcher];
            var length = match.TextLength;
            // 同じ長さのマッチがまだ無い時
            if (stateDict.ContainsKey(length) == false)
            {
                stateDict.Add(length, new HashSet<Match>());
            }
            stateDict[length].Add(match);
            // 逆引き辞書にもレコードを追加する
            _staying_MatchToPos.Add(match, matcher);
        }

        /// <summary>
        /// マッチを管理リストから削除する
        /// </summary>
        /// <param name="match"></param>
        public void Running_RemoveMatch(Match match)
        {
            if (_running_MatchToPos.ContainsKey(match))
            {
                _running_MatchToPos.Remove(match);
            }
        }

        /// <summary>
        /// マッチを管理リストから削除する
        /// </summary>
        /// <param name="match">マッチ</param>
        public void Staying_RemoveMatch(Match match)
        {
            // 停止中マッチではない時(走行中マッチである可能性もある)
            if (_staying_MatchToPos.ContainsKey(match) == false)
            {
                // 何もしない
                return;
            }

            // マッチからマッチャーを得る
            var matcher = _staying_MatchToPos[match];
            var length = match.TextLength;
            _staying_MatchToPos.Remove(match);
            _staying_PosToMatch[matcher][length].Remove(match);
        }

        /// <summary>
        /// ルートまで到達できたマッチ群
        /// </summary>
        private List<RootMatch> _finishedMatches = new List<RootMatch>();

        /// <summary>
        /// ルートまで到達できたマッチ群
        /// </summary>
        public List<RootMatch> FinishedMatches
        {
            get
            {
                return _finishedMatches;
            }
        }




        /// <summary>
        /// マッチの現在位置を取得する。
        /// </summary>
        /// <param name="match">マッチ</param>
        /// <returns>マッチが乗っているマッチャー</returns>
        public Matcher Staying_MatchToPos(Match match)
        {
            if (_staying_MatchToPos.ContainsKey(match))
            {
                return _staying_MatchToPos[match];
            }
            return null;
        }

        /// <summary>
        /// マッチの現在位置を取得する。
        /// </summary>
        /// <param name="match">マッチ</param>
        /// <returns>マッチが乗っているマッチャー</returns>
        public Matcher Running_MatchToPos(Match match)
        {
            if (_running_MatchToPos.ContainsKey(match))
            {
                return _running_MatchToPos[match];
            }
            return null;
        }

        /// <summary>
        /// 入力文字列
        /// </summary>
        private BufferedEnumerator<char> _text;

        /// <summary>
        /// 入力文字列
        /// </summary>
        public BufferedEnumerator<char> Text
        {
            get { return _text; }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="root">マッチャー</param>
        /// <param name="text">入力文字列</param>
        /// <param name="tabCount">タブ文字を空白何個に換算するか</param>
        public Executor(Matcher matcher, IEnumerable<char> text, int tabCount = 4)
        {
            // 列挙可能なだけの入力文字列を、シーク可能な列挙型で包む。
            _text = new BufferedEnumerator<char>(text);

            RootMatcher root = null;
            // 引数のマッチャーがルートマッチャーならそのまま使う。
            if (matcher is RootMatcher tempRoot)
            {
                root = tempRoot;
            }
            // ルートマッチャーで無い時はルートマッチャーで包む。
            else
            {
                root = new RootMatcher(matcher);
            }


            // ルートマッチャーの参照を退避する。
            _root = root;

            // ルートマッチャーから末端マッチャーと長さゼロマッチャーを全て洗い出す。
            // (ここで洗い出した末端マッチャーに入力文字を食わせる)
            _terminalList = new HashSet<IReceiveChar>();

            _zeroLengthList = new HashSet<IZeroLength>();

            _beginMatcherList = new HashSet<BuiltInMatcher.BeginMatcher>();

            _endMatcherList = new HashSet<BuiltInMatcher.EndMatcher>();

            _wordBreakMatcherList = new HashSet<BuiltInMatcher.WordBreakMatcher>();

            _indentMatcherList = new HashSet<BuiltInMatcher.IndentMatcher>();

            _dedentMatcherList = new HashSet<BuiltInMatcher.DedentMatcher>();

            _errorDedentMatcherList = new HashSet<BuiltInMatcher.ErrorDedentMatcher>();

            _parentToChild = new Dictionary<IHasInner, HashSet<Matcher>>();

            _childToParent = new Dictionary<Matcher, HashSet<IHasInner>>();

            var knownMatchers = new HashSet<Matcher>();


            EnumTerminals(_root);

        }

        /// <summary>
        /// ルートからたどって末端マッチをリストに格納する
        /// </summary>
        /// <param name="root"></param>
        private void EnumTerminals(RootMatcher root)
        {
            // var knownMatchers = new HashSet<Matcher>();
            // 既知の子持ちマッチャー(無限再帰防止用)
            var knownHasInners = new HashSet<IHasInner>();

            //Debug.Write(OutputMatcher(root));

            EnumTerminalsBody(root);

            void EnumTerminalsBody(IHasInner parent)
            {
                IHasInner parentOriginal = (IHasInner)(((Matcher)parent).Original);
                if (knownHasInners.Contains(parentOriginal))
                {
                    return;
                }

                knownHasInners.Add(parent);

                foreach (var child in parent.Inners)
                {
                    var childOriginal = child.Original;

                    // 親マッチャーから子マッチャーを手繰るリストに追加する
                    if (_parentToChild.ContainsKey(parentOriginal) == false)
                    {
                        _parentToChild.Add(parentOriginal, new HashSet<Matcher>());
                    }
                    _parentToChild[parentOriginal].Add(childOriginal);

                    // 子マッチャーから親マッチャーを手繰るリストに追加する
                    if (_childToParent.ContainsKey(childOriginal) == false)
                    {
                        _childToParent.Add(childOriginal, new HashSet<IHasInner>());
                    }
                    _childToParent[childOriginal].Add(parentOriginal);


                    // ループマッチャーは長さゼロとも子持ちとも解釈する
                    if (childOriginal is LoopMatcher loop)
                    {
                        _zeroLengthList.Add(loop);
                        EnumTerminalsBody(loop);
                    }
                    // 子持ちマッチャーの時
                    else if (childOriginal is IHasInner hasInner)
                    {
                        EnumTerminalsBody(hasInner);
                    }
                    // 文字列先頭マッチャーは先頭リストに追加する
                    else if (childOriginal is BuiltInMatcher.BeginMatcher begin)
                    {
                        _beginMatcherList.Add(begin);
                    }
                    // 文字列終端マッチャーは終端リストに追加する
                    else if (childOriginal is BuiltInMatcher.EndMatcher end)
                    {
                        _endMatcherList.Add(end);
                    }
                    // 単語区切りマッチャーは単語区切りリストに追加する
                    else if (childOriginal is BuiltInMatcher.WordBreakMatcher wb)
                    {
                        _wordBreakMatcherList.Add(wb);
                    }
                    // インデントマッチャーはインデントリストに追加する
                    else if (childOriginal is BuiltInMatcher.IndentMatcher indent)
                    {
                        _indentMatcherList.Add(indent);
                    }
                    // デデントマッチャーはデデントリストに追加する
                    else if (childOriginal is BuiltInMatcher.DedentMatcher dedent)
                    {
                        _dedentMatcherList.Add(dedent);
                    }
                    else if (childOriginal is IReceiveChar others)
                    {
                        _terminalList.Add(others);
                    }
                    else if (childOriginal is IZeroLength zero)
                    {
                        _zeroLengthList.Add(zero);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            }


        }

        #region マッチャー木構造出力処理(デバッグ用)
        public string OutputChildToParent()
        {
            var sb = new StringBuilder();

            foreach (var item in _childToParent)
            {
                var current = (Matcher)(item.Key);

                var originalUniqID = "";
                if (current.IsOriginal)
                {
                    originalUniqID = "★";
                }
                else
                {
                    originalUniqID = current.Original.UniqID;
                }
                sb.AppendLine(current.GetType().Name + " " + ((Matcher)current).UniqID + " " + originalUniqID);

                foreach (var listItem in item.Value)
                {
                    var parent = (Matcher)listItem;

                    if (parent.IsOriginal)
                    {
                        originalUniqID = "★";
                    }
                    else
                    {
                        originalUniqID = parent.Original.UniqID;
                    }
                    sb.AppendLine("  " + parent.GetType().Name + " " + ((Matcher)parent).UniqID + " " + originalUniqID);
                }
            }
            return sb.ToString();
        }

        public string OutputTree(Matcher root)
        {
            var sb = new StringBuilder();
            HashSet<RecursionMatcher> recList = new HashSet<RecursionMatcher>();
            OutputTree_Body((Matcher)root, "");
            return sb.ToString();

            void OutputTree_Body(Matcher current, string indent)
            {
                if (current is RecursionMatcher rec)
                {
                    if (recList.Contains(rec))
                    {
                        return;
                    }
                    recList.Add(rec);
                }

                var isOriginal = current.IsOriginal;

                var originalUniqID = "";
                if (current.IsOriginal)
                {
                    originalUniqID = "★";
                }
                else
                {
                    originalUniqID = current.Original.UniqID;
                }


                if (current is IHasInner hasInner)
                {
                    sb.AppendLine(indent + current.GetType().Name + " " + ((Matcher)current).UniqID + " " + originalUniqID);

                    foreach (var inner in _parentToChild[hasInner])
                    {
                        OutputTree_Body(inner, indent + "  ");
                    }
                }
                else
                {
                    sb.AppendLine(indent + "\"" + current.ToString() + "\" " + current.UniqID + " " + originalUniqID);
                }
            }
        }

        public static string OutputMatcher(Matcher root)
        {
            var sb = new StringBuilder();
            Output_Body((Matcher)root, "");
            return sb.ToString();

            void Output_Body(Matcher current, string indent)
            {
                var isOriginal = current.IsOriginal;

                var originalUniqID = "";
                if (current.IsOriginal)
                {
                    originalUniqID = "★";
                }
                else
                {
                    originalUniqID = current.Original.UniqID;
                }


                if (current is IHasInner hasInner)
                {
                    if (current != null)
                    {
                        sb.AppendLine(indent + current.GetType().Name + " " + ((Matcher)current).UniqID + " " + originalUniqID);

                        foreach (var inner in hasInner.Inners)
                        {
                            if (inner != null)
                            {
                                Output_Body(inner, indent + "  ");
                            }
                        }
                    }
                }
                else
                {
                    sb.AppendLine(indent + "\"" + current.ToString() + "\" " + current.UniqID + " " + originalUniqID);
                }
            }
        }
        public string OutputRunningMatcher(IHasInner root)
        {
            var sb = new StringBuilder();
            Output_Body(root, "");
            return sb.ToString();

            void Output_Body(IHasInner current, string indent)
            {
                foreach (var inner in _parentToChild[current])
                {
                    sb.AppendLine(indent + inner.DebugName + " " + inner.UniqID);
                    if (inner is IHasInner hasInner)
                    {
                        Output_Body(hasInner, indent + "  ");
                    }
                }
            }
        }
        #endregion

        #region Matchのインスタンス管理

        /// <summary>参照カウント</summary>
        private Dictionary<Match, int> _referenceCount
            = new Dictionary<Match, int>();

        /// <summary>
        /// マッチの参照カウントをプラスする
        /// </summary>
        /// <param name="match">マッチ</param>
        public void ReferenceCountPlus(Match match)
        {
            if (_referenceCount.ContainsKey(match) == false)
            {
                _referenceCount.Add(match, 0);
            }
            _referenceCount[match] += 1;
        }

        /// <summary>
        /// マッチの参照カウントをマイナスする。
        /// 参照カウントがゼロになったマッチは完全に削除され、
        /// 同一の開始インデックス
        /// </summary>
        /// <param name="match">マッチ</param>
        public void ReferenceCountMinus(Match match)
        {
            // マッチの参照カウントを減らす
            _referenceCount[match] -= 1;

            // マッチの参照カウントがゼロになった時
            if (_referenceCount[match] == 0)
            {
                // 開始インデックスを取得する
                int index = match.TextBegin;
                // 開始リストからマッチを削除する
                _beginList[index].Remove(match);

                // マッチが停止中マッチの時
                if (_staying_MatchToPos.ContainsKey(match))
                {
                    // 停止中マッチリストから削除する
                    Staying_RemoveMatch(match);
                }
                else
                {
                    // 走行中マッチリストから削除する
                    Running_RemoveMatch(match);
                }

                // このインデックスから開始するマッチが無くなった時
                // (さっき消したマッチが最後の１個だった時)
                if (_beginList[index].Count == 0)
                {
                    // このインデックスで終了するマッチ全てを調べる
                    foreach (var item in _endList[index])
                    {
                        // 右側が未結合(後続を待っている)の時
                        if (item.RightConnection == Match.ConnectionStatus.NotConnect)
                        {
                            // item をアンラップする
                            item.UnWrap(this);
                        }
                    }
                }

                // 参照カウントリストからマッチを削除する。
                _referenceCount.Remove(match);
            }
        }

        /// <summary>
        /// 開始リスト(このインデックスから開始するマッチのリスト)
        /// </summary>
        private Dictionary<int, HashSet<Match>> _beginList
            = new Dictionary<int, HashSet<Match>>();

        /// <summary>
        /// 終了リスト(このインデックスで終了するマッチのリスト)
        /// </summary>
        private Dictionary<int, HashSet<Match>> _endList
            = new Dictionary<int, HashSet<Match>>();

        /// <summary>
        /// マッチを終了リスト・開始リストに登録する。
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <param name="match">登録するマッチ</param>
        public void RegisterMatch(Match match)
        {
            //
            // 開始リストにHashSetを作ってマッチを登録する。
            //
            var beginIndex = match.TextBegin;
            if (_beginList.ContainsKey(beginIndex) == false)
            {
                _beginList.Add(beginIndex, new HashSet<Match>());
            }
            _beginList[match.TextBegin].Add(match);

            //
            // 終了リストにHashSetを作ってマッチを登録する。
            //
            var endIndex = match.TextEnd;
            if (_endList.ContainsKey(endIndex) == false)
            {
                _endList.Add(endIndex, new HashSet<Match>());
            }
            _endList[match.TextEnd].Add(match);
        }



        #endregion

        #region GetEnumerator(入力を１文字ずつ消費して、ルートまでたどり着けたマッチの有無を返す。)
        /// <summary>
        /// 入力を１文字ずつ消費して、ルートまでたどり着けたマッチの有無を返す。
        /// </summary>
        /// <returns>ルートまでたどり着けたマッチの有無</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _getEnumerator_Body();
        }

        /// <summary>
        /// 入力を１文字ずつ消費して、ルートまでたどり着けたマッチの有無を返す。
        /// </summary>
        /// <returns>ルートまでたどり着けたマッチの有無</returns>
        public IEnumerator<Match> GetEnumerator()
        {
            return _getEnumerator_Body();
        }

        /// <summary>
        /// マッチング処理の基幹部分。
        /// 入力を１文字ずつ消費して、ルートまでたどり着けたマッチの有無を返す。
        /// </summary>
        /// <returns>
        /// ルートまでたどり着けたマッチの有無を返信する。
        /// 一旦１個以上になったら減らないのでずっとtrueを返し続ける。
        /// </returns>
        private IEnumerator<Match> _getEnumerator_Body()
        {
            var index = 0;
            int currentFinishedCount = 0;

            _blankList = new SortedList<int, TextRange>();

            foreach(var token in new TokenStream(_text))
            {
                index = token.Begin;

                // このインデックスから開始するマッチを保存するリストを作成する。
                // (削除用)
                if (_beginList.ContainsKey(index) == false)
                {
                    _beginList.Add(index, new HashSet<Match>());
                }

                // このインデックスで終了するマッチを保存するリストを作成する。
                // (削除用)
                if (_endList.ContainsKey(index) == false)
                {
                    _endList.Add(index, new HashSet<Match>());
                }

                // ルートまで上がったマッチの数を取得しておく
                currentFinishedCount = _finishedMatches.Count;

                var range = token.Range;
                var rangeBegin = range.Begin;

                switch (token.Kind)
                {
                // 開始トークンの時
                case TokenStream.TokenKind.Begin:
                    // 全てのBeginマッチャーに入力文字を食わせる。
                    foreach (var beginMatcher in _beginMatcherList)
                    {
                        Match beginMatch = beginMatcher.OnZeroLength(this, rangeBegin);
                        if (beginMatch == null) { continue; }
                        Running_SetMatchPos(beginMatch, beginMatcher);
                    }
                    break;

                // 単語区切りトークンの時
                case TokenStream.TokenKind.WordBegin:
                case TokenStream.TokenKind.WordEnd:
                    // 全てのWordBreakマッチャーに入力文字を食わせる。
                    // (文字列先頭も単語区切りと見なす為)
                    foreach (var wordBreakMatcher in _wordBreakMatcherList)
                    {
                        Match wordBreakMatch = wordBreakMatcher.OnZeroLength(this, rangeBegin);
                        if (wordBreakMatch == null) { continue; }
                        Running_SetMatchPos(wordBreakMatch, wordBreakMatcher);
                    }
                    break;
                // 長さゼロトークンの時
                case TokenStream.TokenKind.ZeroLength:
                    // 全ての長さ無しマッチャーに入力文字を食わせる。
                    foreach (var zeroLengthMatcher in _zeroLengthList)
                    {
                        Match zeroLengthMatch = zeroLengthMatcher.OnZeroLength(this, rangeBegin);
                        if (zeroLengthMatch == null) { continue; }
                        Running_SetMatchPos(zeroLengthMatch, (Matcher)zeroLengthMatcher);
                    }
                    break;
                // 空白トークンの時
                case TokenStream.TokenKind.SpaceArray:
                    
                    // 空白をブランクリストに登録する
                    _blankList.Add(range.End, range);

                    for(var i = rangeBegin; i < range.End; i++)
                    {
                        // 全ての末端マッチャーに入力文字を食わせる。
                        foreach (var charMatcher in _terminalList)
                        {
                            Match charMatch = charMatcher.ReceiveChar(this, i);
                            if (charMatch == null) { continue; }

                            Running_SetMatchPos(charMatch, (Matcher)charMatcher);
                        }

                        // 全ての走行待ちマッチを全て走らせる。
                        RunAllMatch();
                    }

                    //// 空白マッチャーに空白を通知してマッチを作らせる
                    //foreach (var spaceMatcher in _spaceMatcherList)
                    //{
                    //    Match spaceMatch = spaceMatcher.ReciveSpace(this, range);
                    //    if (spaceMatch == null) { continue; }
                    //    Running_SetMatchPos(spaceMatch, (Matcher)spaceMatcher);
                    //}
                    break;

                // 改行トークンの時
                case TokenStream.TokenKind.Cr:
                case TokenStream.TokenKind.Lf:
                    // 改行をブランクリストに登録する
                    _blankList.Add(range.End, range);

                    // 全ての末端マッチャーに入力文字を食わせる。
                    foreach (var charMatcher in _terminalList)
                    {
                        Match charMatch = charMatcher.ReceiveChar(this, rangeBegin);
                        if (charMatch == null) { continue; }

                        Running_SetMatchPos(charMatch, (Matcher)charMatcher);
                    }
                    break;

                // インデントトークンの時
                case TokenStream.TokenKind.Indent:
                    // 全てのインデントマッチャーに入力文字を食わせる。
                    foreach (var indentMatcher in _indentMatcherList)
                    {
                        Match indentMatch = indentMatcher.CreateMatch(this, range.Begin, range.End);
                        if (indentMatch == null) { continue; }
                        Running_SetMatchPos(indentMatch, (Matcher)indentMatcher);
                    }
                    break;

                // デデントトークンの時
                case TokenStream.TokenKind.Dedent:
                    // 全てのデデントマッチャーに指定個数だけデデント(長さゼロ)を食わせる。
                    foreach (var dedentMatcher in _dedentMatcherList)
                    {
                        Match dedentMatch = dedentMatcher.CreateMatch(this, range.Begin, range.End);
                        if (dedentMatch == null) { continue; }
                        Running_SetMatchPos(dedentMatch, (Matcher)dedentMatcher);
                    }
                    break;

                // エラーデデントトークンの時
                case TokenStream.TokenKind.DedentError:
                    foreach (var errorDedentMatcher in _errorDedentMatcherList)
                    {
                        Match spaceMatch =
                                new ErrorDedentMatch(this, range.Begin, range.End, errorDedentMatcher);
                        Running_SetMatchPos(spaceMatch, (Matcher)errorDedentMatcher);
                    }
                    break;
                // 通常文字トークンの時
                case TokenStream.TokenKind.OneChar:
                case TokenStream.TokenKind.WordChar:
                    // 全ての末端マッチャーに入力文字を食わせる。
                    foreach (var charMatcher in _terminalList)
                    {
                        Match charMatch = charMatcher.ReceiveChar(this, rangeBegin);
                        if (charMatch == null) { continue; }

                        Running_SetMatchPos(charMatch, (Matcher)charMatcher);
                    }
                    break;
                // 終了トークンの時
                case TokenStream.TokenKind.End:
                    // 全てのEndマッチャーに入力文字を食わせる。
                    foreach (var endMatcher in _endMatcherList)
                    {
                        Match endMatch = endMatcher.OnZeroLength(this, rangeBegin);
                        if (endMatch == null) { continue; }
                        Running_SetMatchPos(endMatch, endMatcher);
                    }
                    break;
                }


                // 全ての走行待ちマッチを全て走らせる。
                RunAllMatch();


                // ルートに達したマッチ群から今回の増分だけ返信する
                for (var i = currentFinishedCount; i < _finishedMatches.Count; i++)
                {
                    yield return _finishedMatches[i];
                }

                //// ルートまで達したマッチの増分を計算する。
                //int gain = _finishedMatches.Count - currentFinishedCount;

                //// 今回でルートに達したマッチ群を得る。
                //RootMatch[] currentFinished = new RootMatch[gain];
                //_finishedMatches.CopyTo
                //    (currentFinishedCount, currentFinished, 0, gain);

                //// ルートまで到達できたマッチの数を返信する。
                //yield return new ExecuteResult
                //{
                //    // インデックス
                //    Index = index,
                //    //// 入力文字
                //    //InputCharacter = c,
                //    // ルートまで達したマッチの増分
                //    CurrentFinishedCount = gain,
                //    // ルートまで達したマッチの有無
                //    MatchExists = _finishedMatches.Count > 0,
                //    // 今回のルートに達したマッチ群
                //    CurrentFinished = currentFinished
                //};
            }

        }

        /// <summary>
        /// 未走行のマッチを全て走らせる
        /// </summary>
        void RunAllMatch()
        {
            List<Match> matches = new List<Match>();
            EasyDictionary2<Matcher, Match, Match> pairs =
                new EasyDictionary2<Matcher, Match, Match>();
            EasyDictionary1<Matcher, Match> loops =
                new EasyDictionary1<Matcher, Match>();

            // 未走行マッチが無くなるまで繰り返す
            while (_running_MatchToPos.Count > 0)
            {
                int currentLength = int.MaxValue;

                var keys = _running_MatchToPos.Keys;
                if (keys.Count == 0) { break; }

                var keyList = new List<Match>(keys);

                // 長さが同じ最初の一団だけ取得する
                foreach (Match match in keyList)
                {
                    if (currentLength == int.MaxValue)
                    {
                        currentLength = match.TextLength;
                    }

                    if (currentLength != match.TextLength)
                    {
                        break;
                    }

                    if (match is PairMatch pair)
                    {
                        //pairs[pos][pair.Left] = pair;

                        // pairマッチの現在位置を得る
                        var pos = _running_MatchToPos[pair];

                        // 既に同じマッチャー上に同じ長さのpairマッチが居る時
                        if (pairs.ContainsKey(pos, pair.Left))
                        {
                            // 統合したマッチで上書きする
                            pairs[pos][pair.Left] = pairs[pos][pair.Left].Unit(this, pair);
                        }
                        else
                        {
                            pairs[pos][pair.Left] = pair;
                        }

                    }
                    else if (match is LoopMatch loop)
                    {
                        // loopマッチの現在位置を得る
                        var pos = _running_MatchToPos[loop];

                        // 既に同じマッチャー上に同じ長さのLoopマッチが居る時
                        if (loops.ContainsKey(pos))
                        {
                            // 統合したマッチで上書きする
                            loops[pos] = loops[pos].Unit(this, loop);
                        }
                        else
                        {
                            loops[pos] = loop;
                        }
                    }
                    else
                    {
                        matches.Add(match);
                    }

                }

                // 位置・文字列範囲の等しいマッチを統合する。
                // Pairマッチの統合は、長さが同じだけの場合と、
                // 長さのみならずLeftまで同じ場合がある。

                // まず型Pair・Loop・その他で分ける

                foreach (var match in matches)
                {
                    var pos = _running_MatchToPos[match];

                    // 走行中マッチ位置リストから match を消す
                    Running_RemoveMatch(match);

                    RunMatch(pos, match);
                }
                foreach (var match in pairs.Values)
                {
                    var pos = _running_MatchToPos[match];

                    // 走行中マッチ位置リストから match を消す
                    Running_RemoveMatch(match);

                    RunMatch(pos, match);
                }
                foreach (var match in loops.Values)
                {
                    var pos = _running_MatchToPos[match];

                    // 走行中マッチ位置リストから match を消す
                    Running_RemoveMatch(match);

                    RunMatch(pos, match);
                }

                matches.Clear();
                pairs.Clear();
                loops.Clear();
            }
        }

        /// <summary>
        /// マッチを走らせる
        /// </summary>
        /// <param name="currentPos"></param>
        /// <param name="runningMatch"></param>
        void RunMatch(Matcher currentPos, Match runningMatch)
        {

            // 元のマッチの長さを保持しておく
            int currentLength = runningMatch.TextLength;

            // runningMatchが送られるべき親マッチャーのリストを取得する
            HashSet<IHasInner> parents = _childToParent[currentPos];

            foreach (var parent in parents)
            {
                // 親マッチャーにマッチを食わせる
                // Loopマッチャーは複数マッチを返す可能性がある
                var results = parent.ReceiveMatch(this, runningMatch);

                if (results == null)
                {
                    continue;
                }
                foreach (var result in results)
                {
                    if (result == null)
                    {
                        continue;
                    }

                    // 元のマッチがそのまま上がってきた時
                    if (result == runningMatch)
                    {
                        // 走らせる
                        RunMatch((Matcher)parent, result);
                    }
                    // 元のマッチを取り込んだマッチが上がってきた時
                    else
                    {
                        // 元のマッチは走り終わったので走行中リストから削除する
                        _running_MatchToPos.Remove(runningMatch);

                        // 元のマッチと長さが同じ時
                        if (result.TextLength == currentLength)
                        {
                            // ルートマッチなら走行終了
                            if (result is RootMatch root)
                            {
                                _running_MatchToPos.Remove(root);
                                _finishedMatches.Add(root);
                            }
                            // ルートマッチではない時
                            else
                            {
                                // 走らせる
                                RunMatch((Matcher)parent, result);
                            }
                        }
                        // 長さが増えている時
                        else
                        {
                            // できたてホヤホヤで位置が未設定なので
                            // 走行中マッチ位置リストに追加する
                            Running_SetMatchPos(result, (Matcher)parent);
                        }
                    }
                }
            }
        }


        public void WriteTreeText()
        {
            Matcher inner = null;
            foreach (var m in _root.Inners)
            {
                inner = m;
            }
            Debug.WriteLine("index = " + _text.CurrentIndex.ToString());
            Debug.WriteLine(inner.ToTreeText(this));

        }
        #endregion


        #region マッチの連続性判定

        /// <summary>
        /// マッチの連続性を判定する。
        /// 間に長さピッタリのブランクが収まる場合も連続するとみなす。
        /// </summary>
        /// <param name="left">左側のマッチ</param>
        /// <param name="right">右側のマッチ</param>
        /// <returns></returns>
        public bool IsMatchContinuing(Match left, Match right)
        {
            return IsMatchContinuing(left.TextEnd, right.TextBegin);
        }

        /// <summary>
        /// マッチの連続性を判定する。
        /// 間に長さピッタリのブランクが収まる場合も連続するとみなす。
        /// </summary>
        /// <param name="leftMatchEnd">左側マッチの終了位置</param>
        /// <param name="rightMatchBegin">右側マッチの開始位置</param>
        /// <returns></returns>
        public bool IsMatchContinuing(int leftMatchEnd, int rightMatchBegin)
        {
            if(leftMatchEnd == rightMatchBegin)
            {
                return true;
            }

            var blankRight = rightMatchBegin;

            while(_blankList.ContainsKey(blankRight))
            {
                var currentBlank = _blankList[blankRight];
                var blankLeft = currentBlank.Begin;
                if (leftMatchEnd == blankLeft)
                {
                    return true;
                }
                if( blankLeft < leftMatchEnd)
                {
                    return false;
                }
                blankRight = blankLeft;
            }

            return false;
        }
        #endregion

    }
    #endregion


}
