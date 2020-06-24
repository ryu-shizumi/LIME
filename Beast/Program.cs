using System;
using LIME;

namespace Beast
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            
        }

        static void Perth()
        {
            var underBar = '_'._();
            var underBar01 = underBar._01();

            var nonzerodigit = '1'.To('9');
            var digit = '0'.To('9');

            var bindigit = '0'._() | '1';
            var octdigit = '0'.To('7');
            var hexdigit = digit | 'a'.To('f') | 'A'.To('F');

            var bininteger = '0' + ('b'._() | 'B') + (underBar01+ bindigit)._1Max();
            var octinteger = '0' + ('o'._() | 'O') + (underBar01+ octdigit)._1Max();
            var hexinteger = '0' + ('x'._() | 'X') + (underBar01+ hexdigit)._1Max();

            var decinteger = nonzerodigit + (underBar01 + digit)._0Max() | '0' + (underBar01 + '0')._0Max();
            var integer = decinteger | bininteger | octinteger | hexinteger;


            var digitpart = digit + (underBar01+ digit)._0Max();
            var fraction = '.' + digitpart;
            var exponent = ('e'._() | 'E') + ('+'._() | '-')._01() + digitpart;

            var pointfloat = digitpart._01() + fraction | digitpart + '.';
            var exponentfloat = (digitpart | pointfloat) + exponent;
            var floatnumber = pointfloat | exponentfloat;



            //アルファベット
            var Alphabet = ('A'.To('Z') | 'a'.To('z'));
            // 識別子
            var Identifier = (Alphabet | '_') + (Alphabet | '0'.To('9') | '_')._0Max();

            // マイナス記号
            var Minus = '-'._();

            // 数字
            var Numeric = '0'.To('9');

            // 整数値
            var IntegerLiteral = '0' | (Minus._01() + '1'.To('9') + Numeric._1Max());

            // ゼロで始まっても良い整数値
            var Numerics = Numeric._1Max();

            // 小数部
            var RealPart = '.' + Numerics;

            // 正負の符号
            var Sign = Minus | '+';

            // 指数部
            var ExponentPart = ('e'._() | 'E') + Sign._01() + Numerics;

            // 実数値
            var RealLiteral = IntegerLiteral + RealPart._01() + ExponentPart._01();

            var Cr = '\r'._();
            var Lf = '\n'._();

            // 文字列リテラル(C言語形式)
            var StringLiteral = '"'._() +
                (('\\' + (Cr | Lf).Not) | (Cr | Lf | '\\' | '"').Not)._0Max() +
                '"';

            // 「リテラル値式」(再帰マッチャー)
            var LiteralExp = new RecursionMatcher();

            // 「括弧式」(再帰マッチャー)
            var ParenExp = new RecursionMatcher();

            // 「代入可能式」(再帰マッチャー)
            var AssignableExp = new RecursionMatcher();

            // 「関数呼び出し式」(再帰マッチャー)
            var FunctionCallExp = new RecursionMatcher();

            // 「メンバアクセス式」(再帰マッチャー)
            var MemberAccessExp = new RecursionMatcher();

            // 「インデックスアクセス式」(再帰マッチャー)
            var IndexAccessExp = new RecursionMatcher();

            // 「後置デクリメント」
            var PostDecrementExp = AssignableExp + "--";

            // 「後置インクリメント」
            var PostIncrementExp = AssignableExp + "++";

            // 優先順位１式
            var Priority1Exp = 
                LiteralExp |        // リテラル
                Identifier |        // 識別子
                ParenExp |          // 丸括弧式
                AssignableExp |     // 代入可能式
                FunctionCallExp |   // 関数呼び出し式
                MemberAccessExp |   // メンバアクセス式
                IndexAccessExp |    // インデックスアクセス式
                PostDecrementExp |  // 後置デクリメント
                PostIncrementExp;   // 後置インクリメント


            //
            // 優先度２
            //

            // 「前置デクリメント」
            var PreDecrementExp = "--" + AssignableExp;

            // 「前置インクリメント」
            var PreIncrementExp = "++" + AssignableExp;

            // 「前置マイナス」(再帰マッチャー)
            var PreMinusExp = new RecursionMatcher();

            // 「前置プラス」(再帰マッチャー)
            var PrePlusExp = new RecursionMatcher();

            // 優先順位２式
            var Priority2Exp = PreDecrementExp | PreIncrementExp | PreMinusExp | PrePlusExp;

            // 優先順位２以上式
            var PriorityAbove2Exp = Priority1Exp | Priority2Exp;


            //
            // 優先度３
            //

            // 「乗除算式」(再帰マッチャー)
            var MulDivExp = new RecursionMatcher();

            // 優先順位３式
            var Priority3Exp = MulDivExp;

            // 優先順位３以上式
            var PriorityAbove3Exp = PriorityAbove2Exp | Priority3Exp;


            //
            // 優先度４
            //

            // 「加減算式」(再帰マッチャー)
            var AddSubExp = new RecursionMatcher();

            // 優先順位４式
            var Priority4Exp = AddSubExp;

            // 優先順位４式
            var PriorityAbove4Exp = PriorityAbove3Exp | Priority4Exp;

            //
            // (優先順位の低い演算子を増やしたい場合はここに挿入する。)
            //

            // 式の全て
            var Exp = PriorityAbove4Exp;

            //
            // 優先度９９９
            //

            // 代入演算文
            var AssignStatement = AssignableExp + '=' + Exp;


            //
            // 以下、中身が未設定なマッチャーの中身を設定
            //

            // 括弧式の中身
            ParenExp.Inner = '(' +
                (
                // 代入可能式を除く優先順位１式
                LiteralExp | Identifier | ParenExp | FunctionCallExp |
                IndexAccessExp | MemberAccessExp | PostDecrementExp | PostIncrementExp |

                // 優先順位２以下の全ての式
                Priority2Exp | Priority3Exp | Priority4Exp
                )
                 + ')';

            // 代入可能式の中身
            AssignableExp.Inner = Identifier | MemberAccessExp | IndexAccessExp | ('(' + AssignableExp + ')');

            // 関数呼び出し式の中身
            FunctionCallExp.Inner = (FunctionCallExp | IndexAccessExp | AssignableExp) + '(' + Exp._0Max(',') + ')';

            // インデックスアクセス式の中身
            IndexAccessExp.Inner = (FunctionCallExp | IndexAccessExp | AssignableExp) + '[' + Exp._1Max(',') + ']';

            // メンバアクセス式の中身
            MemberAccessExp.Inner = Priority1Exp + '.' + Identifier;

            // 前置マイナス式の中身
            PreMinusExp.Inner = '-' + (Priority1Exp | PreDecrementExp | PreIncrementExp | PrePlusExp);

            // 前置プラス式の中身
            PrePlusExp.Inner = '+' + (Priority1Exp | PreDecrementExp | PreIncrementExp | PreMinusExp);

            // 乗除算式の中身
            MulDivExp.Inner = PriorityAbove3Exp + ('*'._() | '/') + PriorityAbove2Exp;

            // 加減算式の中身
            AddSubExp.Inner = PriorityAbove4Exp + ('+'._() | '-') + PriorityAbove3Exp;

            // Expression_00

            //var Expression00 = Expression


            // 乗除



            // 加減

            // 式 Expression
        }


    }
}
