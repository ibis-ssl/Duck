"use strict";

module.exports = function noUnresolvedPlaceholderMarker(context, options = {}) {
  const { Syntax, RuleError, report, getSource } = context;
  const markers = options.markers || ["TBD", "WIP"];
  const markerPattern = new RegExp(`\\b(${markers.map(escapeRegExp).join("|")})\\b`);

  return {
    [Syntax.Str](node) {
      const text = getSource(node);
      const match = markerPattern.exec(text);

      if (match) {
        report(
          node,
          new RuleError(`未解決 placeholder marker '${match[1]}' を解消してください。`, {
            index: match.index
          })
        );
      }
    }
  };
};

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
