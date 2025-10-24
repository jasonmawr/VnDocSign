using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Interfaces.Integration
{
    public interface IPdfConverter
    {
        /// <summary>
        /// Convert a DOCX file to PDF and return absolute pdf path.
        /// </summary>
        /// <param name="docxPath">Absolute path to DOCX file</param>
        /// <param name="ct"></param>
        Task<string> ConvertDocxToPdfAsync(string docxPath, CancellationToken ct = default);
    }
}

