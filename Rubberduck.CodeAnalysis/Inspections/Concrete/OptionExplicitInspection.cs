using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Rubberduck.Inspections.Abstract;
using Rubberduck.Inspections.Results;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Inspections.Abstract;
using Rubberduck.Resources.Inspections;
using Rubberduck.Parsing.VBA;
using Rubberduck.VBEditor;
using Rubberduck.Inspections.Inspections.Extensions;

namespace Rubberduck.Inspections.Concrete
{
    /// <summary>
    /// Flags modules that omit Option Explicit.
    /// </summary>
    /// <why>
    /// This option makes variable declarations mandatory. Without it, a typo gets compiled as a new on-the-spot Variant/Empty variable with a new name. 
    /// Omitting this option amounts to refusing the little help the VBE can provide with compile-time validation.
    /// </why>
    /// <example hasResults="true">
    /// <![CDATA[
    ///
    /// 
    /// Public Sub DoSomething()
    ///     ' ...
    /// End Sub
    /// ]]>
    /// </example>
    /// <example hasResults="false">
    /// <![CDATA[
    /// Option Explicit
    /// 
    /// Public Sub DoSomething()
    ///     ' ...
    /// End Sub
    /// ]]>
    /// </example>
    public sealed class OptionExplicitInspection : ParseTreeInspectionBase
    {
        public OptionExplicitInspection(RubberduckParserState state)
            : base(state)
        {
            Listener = new MissingOptionExplicitListener();
        }

        public override IInspectionListener Listener { get; }

        protected override IEnumerable<IInspectionResult> DoGetInspectionResults()
        {
            return Listener.Contexts
                .Where(context => !context.IsIgnoringInspectionResultFor(State.DeclarationFinder, AnnotationName))
                .Select(context => new QualifiedContextInspectionResult(this,
                    string.Format(InspectionResults.OptionExplicitInspection, context.ModuleName.ComponentName),
                    context));
        }

        public class MissingOptionExplicitListener : VBAParserBaseListener, IInspectionListener
        {
            private readonly List<QualifiedContext<ParserRuleContext>> _contexts = new List<QualifiedContext<ParserRuleContext>>();
            public IReadOnlyList<QualifiedContext<ParserRuleContext>> Contexts => _contexts;

            public QualifiedModuleName CurrentModuleName { get; set; }

            public void ClearContexts()
            {
                _contexts.Clear();
            }

            public override void ExitModuleDeclarations([NotNull] VBAParser.ModuleDeclarationsContext context)
            {
                var hasOptionExplicit = false;
                foreach (var element in context.moduleDeclarationsElement())
                {
                    if (element.moduleOption() is VBAParser.OptionExplicitStmtContext)
                    {
                        hasOptionExplicit = true;
                    }
                }

                if (!hasOptionExplicit)
                {
                    _contexts.Add(new QualifiedContext<ParserRuleContext>(CurrentModuleName, (ParserRuleContext)context.Parent));
                }
            }
        }
    }
}
