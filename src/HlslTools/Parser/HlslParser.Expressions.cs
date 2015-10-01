﻿using System.Collections.Generic;
using System.Diagnostics;
using HlslTools.Diagnostics;
using HlslTools.Syntax;

namespace HlslTools.Parser
{
    internal partial class HlslParser
    {
        public ExpressionSyntax ParseExpression()
        {
            return ParseSubExpression(0);
        }

        private ExpressionSyntax ParseSubExpression(uint precedence)
        {
            if (Current.Kind == SyntaxKind.CompileKeyword)
            {
                var compile = Match(SyntaxKind.CompileKeyword);
                var shaderTarget = Match(SyntaxKind.IdentifierToken);
                var shaderFunctionName = ParseIdentifier();
                var shaderFunction = new InvocationExpressionSyntax(shaderFunctionName, ParseParenthesizedArgumentList(false));
                return new CompileExpressionSyntax(compile, shaderTarget, shaderFunction);
            }

            ExpressionSyntax leftOperand;
            SyntaxKind opKind;

            // No left operand, so we need to parse one -- possibly preceded by a
            // unary operator.
            var tk = Current.Kind;
            if (SyntaxFacts.IsPrefixUnaryExpression(tk))
            {
                opKind = SyntaxFacts.GetPrefixUnaryExpression(tk);
                leftOperand = ParsePrefixUnaryExpression(opKind);
            }
            else
            {
                // Not a unary operator - get a primary expression.
                leftOperand = ParseTerm();
            }

            while (true)
            {
                // We either have a binary or assignment operator here, or we're finished.
                tk = Current.Kind;

                var isAssignmentOperator = false;
                if (SyntaxFacts.IsBinaryExpression(tk) 
                    && (!_greaterThanTokenIsNotOperator || tk != SyntaxKind.GreaterThanToken)
                    && ((tk != SyntaxKind.GreaterThanToken || !_allowGreaterThanTokenAroundRhsExpression) && Lookahead.Kind != SyntaxKind.SemiToken))
                {
                    opKind = SyntaxFacts.GetBinaryExpression(tk);
                }
                else if (SyntaxFacts.IsAssignmentExpression(tk))
                {
                    opKind = SyntaxFacts.GetAssignmentExpression(tk);
                    isAssignmentOperator = true;
                }
                else
                {
                    break;
                }

                var newPrecedence = SyntaxFacts.GetOperatorPrecedence(opKind);

                Debug.Assert(newPrecedence > 0); // All binary operators must have precedence > 0!

                // Check the precedence to see if we should "take" this operator
                if (newPrecedence < precedence)
                    break;

                // Same precedence, but not right-associative -- deal with this "later"
                if (newPrecedence == precedence && !SyntaxFacts.IsRightAssociative(opKind))
                    break;

                // Precedence is okay, so we'll "take" this operator.
                var opToken = NextToken();

                SyntaxToken lessThanToken = null;
                if (isAssignmentOperator && _allowGreaterThanTokenAroundRhsExpression)
                    lessThanToken = NextTokenIf(SyntaxKind.LessThanToken);

                var rightOperand = ParseSubExpression(newPrecedence);

                SyntaxToken greaterThanToken = null;
                if (lessThanToken != null)
                    greaterThanToken = NextTokenIf(SyntaxKind.GreaterThanToken);

                if (isAssignmentOperator)
                    leftOperand = new AssignmentExpressionSyntax(opKind, leftOperand, opToken, lessThanToken, rightOperand, greaterThanToken);
                else
                    leftOperand = new BinaryExpressionSyntax(opKind, leftOperand, opToken, rightOperand);
            }

            var conditionalPrecedence = SyntaxFacts.GetOperatorPrecedence(SyntaxKind.ConditionalExpression);
            if (tk == SyntaxKind.QuestionToken && precedence <= conditionalPrecedence)
            {
                var questionToken = NextToken();

                var colonLeft = ParseSubExpression(conditionalPrecedence);
                var colon = Match(SyntaxKind.ColonToken);

                var colonRight = ParseSubExpression(conditionalPrecedence);
                leftOperand = new ConditionalExpressionSyntax(leftOperand, questionToken, colonLeft, colon, colonRight);
            }

            return leftOperand;
        }

        private ExpressionSyntax ParseTerm()
        {
            ExpressionSyntax expr;

            var tk = Current.Kind;
            switch (tk)
            {
                case SyntaxKind.IdentifierToken:
                    expr = ParseIdentifier();
                    break;
                case SyntaxKind.FalseKeyword:
                case SyntaxKind.TrueKeyword:
                case SyntaxKind.IntegerLiteralToken:
                case SyntaxKind.FloatLiteralToken:
                    expr = new LiteralExpressionSyntax(SyntaxFacts.GetLiteralExpression(Current.Kind), NextToken());
                    break;
                case SyntaxKind.StringLiteralToken:
                {
                    var stringTokens = new List<SyntaxToken> { NextToken() };
                    while (Current.Kind == SyntaxKind.StringLiteralToken)
                        stringTokens.Add(NextToken());
                    expr = new StringLiteralExpressionSyntax(stringTokens);
                    break;
                }
                case SyntaxKind.OpenParenToken:
                    expr = ParseCastOrParenthesizedExpression();
                    break;
                default:
                    if ((SyntaxFacts.IsPredefinedScalarType(tk) && tk != SyntaxKind.UnsignedKeyword && tk != SyntaxKind.VoidKeyword)
                        || SyntaxFacts.IsPredefinedVectorType(tk)
                        || SyntaxFacts.IsPredefinedMatrixType(tk))
                    {
                        if (Lookahead.Kind == SyntaxKind.OpenParenToken)
                        {
                            expr = ParseNumericConstructorInvocationExpression();
                            break;
                        }
                    }

                    expr = CreateMissingIdentifierName();

                    if (tk == SyntaxKind.EndOfFileToken)
                        expr = WithDiagnostic(expr, DiagnosticId.ExpressionExpected);
                    else
                        expr = WithDiagnostic(expr, DiagnosticId.InvalidExprTerm, tk.GetText());

                    break;
            }

            return ParsePostFixExpression(expr);
        }

        private NumericConstructorInvocationExpressionSyntax ParseNumericConstructorInvocationExpression()
        {
            var type = ParseType(false);

            return new NumericConstructorInvocationExpressionSyntax((NumericTypeSyntax)type, ParseParenthesizedArgumentList(true));
        }

        private ExpressionSyntax ParsePostFixExpression(ExpressionSyntax expr)
        {
            Debug.Assert(expr != null);

            while (true)
            {
                var tk = Current.Kind;
                switch (tk)
                {
                    case SyntaxKind.OpenParenToken:
                        expr = new InvocationExpressionSyntax(expr, ParseParenthesizedArgumentList(false));
                        break;

                    case SyntaxKind.OpenBracketToken:
                        expr = new ElementAccessExpressionSyntax(expr,
                            Match(SyntaxKind.OpenBracketToken),
                            ParseExpression(),
                            Match(SyntaxKind.CloseBracketToken));
                        break;

                    case SyntaxKind.PlusPlusToken:
                    case SyntaxKind.MinusMinusToken:
                        expr = new PostfixUnaryExpressionSyntax(SyntaxFacts.GetPostfixUnaryExpression(tk), expr, NextToken());
                        break;

                    case SyntaxKind.DotToken:
                        expr = new MemberAccessExpressionSyntax(expr, NextToken(), ParseIdentifier());
                        break;

                    default:
                        return expr;
                }
            }
        }

        private ExpressionSyntax ParseCastOrParenthesizedExpression()
        {
            Debug.Assert(Current.Kind == SyntaxKind.OpenParenToken);

            var resetPoint = GetResetPoint();

            // We have a decision to make -- is this a cast, or is it a parenthesized
            // expression?  Because look-ahead is cheap with our token stream, we check
            // to see if this "looks like" a cast (without constructing any parse trees)
            // to help us make the decision.
            if (ScanCast())
            {
                // Looks like a cast, so parse it as one.
                Reset(ref resetPoint);
                var openParen = Match(SyntaxKind.OpenParenToken);
                List<ArrayRankSpecifierSyntax> arrayRankSpecifiers;
                var type = ParseTypeForCast(out arrayRankSpecifiers);
                var closeParen = Match(SyntaxKind.CloseParenToken);
                var expr = ParseSubExpression(SyntaxFacts.GetOperatorPrecedence(SyntaxKind.CastExpression));
                return new CastExpressionSyntax(openParen, type, arrayRankSpecifiers, closeParen, expr);
            }

            // Doesn't look like a cast, so parse this as a parenthesized expression.
            {
                Reset(ref resetPoint);
                var openParen = Match(SyntaxKind.OpenParenToken);
                var expression = ParseSubExpression(0);
                var closeParen = Match(SyntaxKind.CloseParenToken);
                return new ParenthesizedExpressionSyntax(openParen, expression, closeParen);
            }
        }

        private bool ScanCast()
        {
            if (Current.Kind != SyntaxKind.OpenParenToken)
                return false;

            NextToken();

            var type = ScanType();
            if (type == ScanTypeFlags.NotType)
                return false;

            if (Current.Kind != SyntaxKind.CloseParenToken)
                return false;

            if (type == ScanTypeFlags.MustBeType)
                return true;

            NextToken();

            // check for ambiguous type or expression followed by disambiguating token.  i.e.
            //
            // "(A)b" is a cast.  But "(A)+b" is not a cast.  
            return (type == ScanTypeFlags.TypeOrExpression) && SyntaxFacts.CanFollowCast(Current.Kind);
        }

        private ArgumentListSyntax ParseParenthesizedArgumentList(bool atLeastOneArg)
        {
            SyntaxToken leftParenToken, rightParenToken;
            SeparatedSyntaxList<ExpressionSyntax> arguments;
            ParseArgumentList(SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken, atLeastOneArg,
                out leftParenToken, out arguments, out rightParenToken);

            return new ArgumentListSyntax(leftParenToken, arguments, rightParenToken);
        }

        private void ParseArgumentList(SyntaxKind openKind, SyntaxKind closeKind, bool atLeastOneArg, out SyntaxToken openToken, out SeparatedSyntaxList<ExpressionSyntax> arguments, out SyntaxToken closeToken)
        {
            openToken = Match(openKind);

            var args = new List<SyntaxNode>();

            if (atLeastOneArg || Current.Kind != closeKind)
            {
                args.Add(ParseExpression());

                while (Current.Kind == SyntaxKind.CommaToken)
                {
                    args.Add(Match(SyntaxKind.CommaToken));
                    args.Add(ParseExpression());
                }
            }

            arguments = new SeparatedSyntaxList<ExpressionSyntax>(args);

            closeToken = Match(closeKind);
        }

        private void ParseArrayRankSpecifiers(List<ArrayRankSpecifierSyntax> list, bool expectSize)
        {
            while (Current.Kind == SyntaxKind.OpenBracketToken)
                list.Add(ParseArrayRankSpecifier(expectSize));
        }

        private TypeSyntax ParseTypeForCast(out List<ArrayRankSpecifierSyntax> arrayRankSpecifiers)
        {
            var type = ParseUnderlyingType(false, false);

            // Now check for arrays.
            arrayRankSpecifiers = new List<ArrayRankSpecifierSyntax>();
            ParseArrayRankSpecifiers(arrayRankSpecifiers, true);

            Debug.Assert(type != null);
            return type;
        }

        private bool IsPossibleExpression()
        {
            var tk = Current.Kind;
            switch (tk)
            {
                case SyntaxKind.FalseKeyword:
                case SyntaxKind.TrueKeyword:
                case SyntaxKind.OpenParenToken:
                case SyntaxKind.IntegerLiteralToken:
                case SyntaxKind.FloatLiteralToken:
                case SyntaxKind.StringLiteralToken:
                case SyntaxKind.IdentifierToken:
                case SyntaxKind.CompileKeyword:
                    return true;
                default:
                    return SyntaxFacts.IsPrefixUnaryExpression(tk)
                           || (SyntaxFacts.IsPredefinedType(Current) && tk != SyntaxKind.VoidKeyword && Lookahead.Kind == SyntaxKind.OpenParenToken)
                           || SyntaxFacts.IsAnyUnaryExpression(tk)
                           || SyntaxFacts.IsBinaryExpression(tk)
                           || SyntaxFacts.IsAssignmentExpression(tk);
            }
        }

        private ExpressionSyntax ParsePrefixUnaryExpression(SyntaxKind unaryExpression)
        {
            var operatorToken = NextToken();
            var operatorPrecedence = SyntaxFacts.GetOperatorPrecedence(unaryExpression);
            var operand = ParseSubExpression(operatorPrecedence);
            return new PrefixUnaryExpressionSyntax(unaryExpression, operatorToken, operand);
        }
    }
}