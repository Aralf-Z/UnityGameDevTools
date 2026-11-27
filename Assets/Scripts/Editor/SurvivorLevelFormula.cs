using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sanmon.SurvivorDemo.Editor
{
    /// <summary> 仅在编辑器内计算等级公式, 不向运行时配置写入表达式. </summary>
    internal sealed class SurvivorLevelFormula
    {
        private readonly string _source;
        private readonly double _lv;
        private int _position;

        private SurvivorLevelFormula(string source, int lv)
        {
            _source = source ?? "";
            _lv = lv;
        }

        public static double Evaluate(string expression, int lv)
        {
            var parser = new SurvivorLevelFormula(expression, lv);
            var value = parser.ParseSum();
            parser.SkipWhitespace();
            if (parser._position != parser._source.Length) throw parser.Error("存在无法识别的内容");
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("lv=" + lv + " 的公式结果必须为有限数字。");
            return value;
        }

        public static int EvaluateExperience(string expression, int lv)
        {
            var value = Evaluate(expression, lv);
            if (value < 0 || value > int.MaxValue)
                throw new ArgumentException("lv=" + lv + " 的累计经验必须在 0–2147483647 之间。");
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private double ParseSum()
        {
            var value = ParseProduct();
            while (true)
            {
                if (Take('+')) value += ParseProduct();
                else if (Take('-')) value -= ParseProduct();
                else return value;
            }
        }

        private double ParseProduct()
        {
            var value = ParseUnary();
            while (true)
            {
                if (Take('*')) value *= ParseUnary();
                else if (Take('/')) value /= ParseUnary();
                else if (Take('%')) value %= ParseUnary();
                else return value;
            }
        }

        private double ParseUnary()
        {
            if (Take('+')) return ParseUnary();
            if (Take('-')) return -ParseUnary();
            var value = ParsePrimary();
            if (Take('^')) value = Math.Pow(value, ParseUnary());
            return value;
        }

        private double ParsePrimary()
        {
            if (Take('('))
            {
                var value = ParseSum();
                Expect(')');
                return value;
            }
            SkipWhitespace();
            if (_position >= _source.Length) throw Error("缺少数值或 lv");
            if (char.IsLetter(_source[_position])) return ParseIdentifier();
            var start = _position;
            while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '.')) _position++;
            if (_position < _source.Length && (_source[_position] == 'e' || _source[_position] == 'E'))
            {
                _position++;
                if (_position < _source.Length && (_source[_position] == '+' || _source[_position] == '-')) _position++;
                while (_position < _source.Length && char.IsDigit(_source[_position])) _position++;
            }
            var number = _source.Substring(start, _position - start);
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)) throw Error("无效数值");
            return result;
        }

        private double ParseIdentifier()
        {
            var start = _position;
            while (_position < _source.Length && char.IsLetter(_source[_position])) _position++;
            var name = _source.Substring(start, _position - start).ToLowerInvariant();
            if (name == "lv" || name == "level") return _lv;
            Expect('(');
            var arguments = new List<double> { ParseSum() };
            while (Take(',')) arguments.Add(ParseSum());
            Expect(')');
            return ApplyFunction(name, arguments);
        }

        private double ApplyFunction(string name, List<double> arguments)
        {
            if (arguments.Count == 1)
            {
                var value = arguments[0];
                return name switch
                {
                    "abs" => Math.Abs(value), "sqrt" => Math.Sqrt(value),
                    "floor" => Math.Floor(value), "ceil" => Math.Ceiling(value),
                    "round" => Math.Round(value, MidpointRounding.AwayFromZero),
                    "log" => Math.Log(value), "exp" => Math.Exp(value),
                    _ => throw Error("未知函数或参数数量错误: " + name)
                };
            }
            if (arguments.Count == 2)
            {
                return name switch
                {
                    "min" => Math.Min(arguments[0], arguments[1]),
                    "max" => Math.Max(arguments[0], arguments[1]),
                    "pow" => Math.Pow(arguments[0], arguments[1]),
                    "log" => Math.Log(arguments[0], arguments[1]),
                    _ => throw Error("未知函数或参数数量错误: " + name)
                };
            }
            throw Error("函数参数数量错误: " + name);
        }

        private bool Take(char token)
        {
            SkipWhitespace();
            if (_position >= _source.Length || _source[_position] != token) return false;
            _position++;
            return true;
        }

        private void Expect(char token)
        {
            if (!Take(token)) throw Error("缺少 '" + token + "'");
        }

        private void SkipWhitespace()
        {
            while (_position < _source.Length && char.IsWhiteSpace(_source[_position])) _position++;
        }

        private ArgumentException Error(string message) => new(message + " (位置 " + (_position + 1) + ")。");
    }
}
