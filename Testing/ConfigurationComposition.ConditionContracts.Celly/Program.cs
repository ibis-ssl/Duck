using ConfigurationComposition.ConditionContracts;
using ConfigurationComposition.ConditionContracts.CellyAdapter;

return args.SequenceEqual(new[] { "--request-probe" })
    ? RequestProbe.Run(new CellyConditionEvaluator())
    : ConditionContractCases.Run(new CellyConditionEvaluator());
