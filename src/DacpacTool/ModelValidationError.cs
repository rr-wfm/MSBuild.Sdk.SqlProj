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
        private readonly DacModelError _modelError;
        private readonly string _sourceName;

        public ModelValidationError(DacModelError modelError, string sourceName)
        {
            _modelError = modelError ?? throw new ArgumentNullException(nameof(modelError));
            _sourceName = sourceName.Replace("MSSQL::", string.Empty);
        }

        public ModelErrorSeverity Severity { get => _modelError.Severity; }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(_sourceName ?? _modelError.SourceName);
            stringBuilder.Append('(');
            stringBuilder.Append(_modelError.Line);
            stringBuilder.Append(',');
            stringBuilder.Append(_modelError.Column);
            stringBuilder.Append("):");
            stringBuilder.Append(_modelError.ErrorType);
            stringBuilder.Append(' ');
            stringBuilder.Append(_modelError.Severity);
            stringBuilder.Append(' ');
            stringBuilder.Append(_modelError.Prefix);
            stringBuilder.Append(_modelError.ErrorCode);
            stringBuilder.Append(": ");
            stringBuilder.Append(_modelError.Message);

            return stringBuilder.ToString();
        }
    }
}
