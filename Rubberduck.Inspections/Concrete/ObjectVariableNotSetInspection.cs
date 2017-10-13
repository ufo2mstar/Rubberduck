﻿using System;
using System.Collections.Generic;
using System.Linq;
using Rubberduck.Inspections.Abstract;
using Rubberduck.Inspections.Results;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Inspections.Abstract;
using Rubberduck.Parsing.Inspections.Resources;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;

namespace Rubberduck.Inspections.Concrete
{
    public sealed class ObjectVariableNotSetInspection : InspectionBase
    {
        public ObjectVariableNotSetInspection(RubberduckParserState state)
            : base(state, CodeInspectionSeverity.Error) {  }

        public override CodeInspectionType InspectionType => CodeInspectionType.CodeQualityIssues;

        protected override IEnumerable<IInspectionResult> DoGetInspectionResults()
        {
            var allInterestingDeclarations =
                VariableRequiresSetAssignmentEvaluator.GetDeclarationsPotentiallyRequiringSetAssignment(State.AllUserDeclarations);

            var candidateReferencesRequiringSetAssignment = 
                allInterestingDeclarations
                    .SelectMany(dec => dec.References)
                    .Where(reference => !IsIgnoringInspectionResultFor(reference, AnnotationName))                    
                    .Where(reference => reference.IsAssignment);

            var referencesRequiringSetAssignment = candidateReferencesRequiringSetAssignment                  
                .Where(reference => VariableRequiresSetAssignmentEvaluator.RequiresSetAssignment(reference, State));

            var objectVariableNotSetReferences = referencesRequiringSetAssignment.Where(FlagIfObjectVariableNotSet);

            return objectVariableNotSetReferences.Select(reference =>
                new IdentifierReferenceInspectionResult(this,
                                     string.Format(InspectionsUI.ObjectVariableNotSetInspectionResultFormat, reference.Declaration.IdentifierName),
                                     State,
                                     reference));
        }

        private bool FlagIfObjectVariableNotSet(IdentifierReference reference)
        {
            var letStmtContext = ParserRuleContextHelper.GetParent<VBAParser.LetStmtContext>(reference.Context);
            var setStmtContext = ParserRuleContextHelper.GetParent<VBAParser.SetStmtContext>(reference.Context);
            var setAssignmentExpression = setStmtContext?.expression()?.GetText();

            return reference.IsAssignment && (letStmtContext != null 
                   || (setAssignmentExpression?.Equals(Tokens.Nothing, StringComparison.InvariantCultureIgnoreCase) ?? false));
        }
    }
}
