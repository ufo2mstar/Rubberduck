﻿using System.Runtime.InteropServices;
using Rubberduck.Parsing.VBA;
using Rubberduck.SmartIndenter;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;

namespace Rubberduck.UI.Command
{
    [ComVisible(false)]
    public class IndentCurrentModuleCommand : CommandBase
    {
        private readonly IVBE _vbe;
        private readonly IIndenter _indenter;
        private readonly RubberduckParserState _state;

        public IndentCurrentModuleCommand(IVBE vbe, IIndenter indenter, RubberduckParserState state)
        {
            _vbe = vbe;
            _indenter = indenter;
            _state = state;

            AddToCanExecuteEvaluation(SpecialEvaluateCanExecute);
        }

        private bool SpecialEvaluateCanExecute(object parameter)
        {
            using (var activePane = _vbe.ActiveCodePane)
            {
                return activePane != null && !activePane.IsWrappingNullReference;
            }
        }

        protected override void OnExecute(object parameter)
        {
            _indenter.IndentCurrentModule();
            if (_state.Status >= ParserState.Ready || _state.Status == ParserState.Pending)
            {
                _state.OnParseRequested(this);
            }
        }
    }
}
