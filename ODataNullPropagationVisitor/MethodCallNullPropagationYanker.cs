﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace OData.Linq
{
    public class MethodCallNullPropagationYanker : ExpressionVisitor {
        private bool _yankingNull;

        protected override Expression VisitUnary(UnaryExpression node) {
            if (_yankingNull &&
                node.NodeType == ExpressionType.Convert &&
                Nullable.GetUnderlyingType(node.Type) == typeof(bool)) {
                return Visit(node.Operand);
            }

            return base.VisitUnary(node);
        }

        protected override Expression VisitBinary(BinaryExpression node) {
            if (_yankingNull) {
                Expression left = Visit(node.Left);
                Expression right = Visit(node.Right);

                if (left == null) {
                    return right;
                }

                if (right == null) {
                    return left;
                }

                if (node.NodeType == ExpressionType.NotEqual &&
                    (IsNullConstant(right) || IsNullConstant(left))) {
                    return null;
                }

                return Expression.MakeBinary(node.NodeType, left, right);
            }
            return base.VisitBinary(node);
        }

        protected override Expression VisitConditional(ConditionalExpression node) {
            Expression expression;
            if (TryRemoveNullPropagation(node, out expression)) {
                return expression;
            }

            if (_yankingNull && IsNullCheck(node.Test)) {
                return Visit(node.IfFalse);
            }

            return base.VisitConditional(node);
        }

        private bool IsNullCheck(Expression expression) {
            if (expression.NodeType != ExpressionType.Equal) {
                return false;
            }

            var binaryExpr = (BinaryExpression)expression;
            return IsNullConstant(binaryExpr.Right);
        }

        private bool TryRemoveNullPropagation(ConditionalExpression node, out Expression condition) {
            condition = null;
            if (node.IfTrue.NodeType != ExpressionType.Constant) {
                return false;
            }

            if (node.Test.NodeType != ExpressionType.Equal) {
                return false;
            }

            var test = (BinaryExpression)node.Test;
            var constantExpr = (ConstantExpression)node.IfTrue;

            if (node.IfFalse.NodeType != ExpressionType.Call) {
                return false;
            }

            var memberExpr = (MethodCallExpression)node.IfFalse;

            _yankingNull = true;
            condition = Visit(memberExpr);
            _yankingNull = false;
            return true;
        }

        private bool IsNullConstant(Expression expression) {
            return expression.NodeType == ExpressionType.Constant &&
                   ((ConstantExpression)expression).Value == null;
        }
    }
}