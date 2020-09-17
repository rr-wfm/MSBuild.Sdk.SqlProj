using System;
using System.Text;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    /// <summary>
    /// A wrapper for <see cref="DacModelError" /> that provides MSBuild compatible output and source document information.
    /// </summary>
    public class ModelValidationError
    {
        private readonly string _sourceName;
        private readonly int _line;
        private readonly int _column;
        private readonly ModelErrorType _errorType;
        private readonly ModelErrorSeverity _severity;
        private readonly string _prefix;
        private readonly int _errorCode;
        private readonly string _message;

        public ModelValidationError(DacModelError modelError, string sourceName)
        {
            if (modelError == null) throw new ArgumentNullException(nameof(modelError));
            _sourceName = String.IsNullOrEmpty(sourceName) ? 
                            modelError.SourceName : 
                            sourceName.Replace("MSSQL::", string.Empty);
            _line = modelError.Line;
            _column = modelError.Column;
            _errorType = modelError.ErrorType;
            _severity = modelError.Severity;
            _prefix = modelError.Prefix;
            _errorCode = modelError.ErrorCode;
            _message = modelError.Message;
        }

        public ModelValidationError(string sourceName, int line, int column, ModelErrorType errorType,
            ModelErrorSeverity severity, string prefix, int errorCode, string message)
        {
            _sourceName = sourceName;
            _line = line;
            _column = column;
            _errorType = errorType;
            _severity = severity;
            _prefix = prefix;
            _errorCode = errorCode;
            _message = message;
        }

        public ModelErrorSeverity Severity { get => _severity; }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(_sourceName);
            stringBuilder.Append('(');
            stringBuilder.Append(_line);
            stringBuilder.Append(',');
            stringBuilder.Append(_column);
            stringBuilder.Append("):");
            stringBuilder.Append(_errorType);
            stringBuilder.Append(' ');
            stringBuilder.Append(_severity);
            stringBuilder.Append(' ');
            stringBuilder.Append(_prefix);
            stringBuilder.Append(_errorCode);
            stringBuilder.Append(": ");
            stringBuilder.Append(_message);

            return stringBuilder.ToString();
        }
    }
}
