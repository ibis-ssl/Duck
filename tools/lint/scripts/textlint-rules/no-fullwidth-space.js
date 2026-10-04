"use strict";

module.exports = function noFullwidthSpace(context) {
  const { Syntax, RuleError, report, getSource } = context;

  return {
    [Syntax.Str](node) {
      const text = getSource(node);
      const index = text.indexOf("\u3000");

      if (index !== -1) {
        report(
          node,
          new RuleError("全角スペースではなく、通常の空白または Markdown の構造で余白を表現してください。", {
            index
          })
        );
      }
    }
  };
};
