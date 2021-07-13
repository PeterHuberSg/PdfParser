/**************************************************************************************

PdfParser
=========

Parse a pdf file or byte array and finds all its tokens.

Written in 2021 by Jürgpeter Huber, Singapore

Contact: https://github.com/PeterHuberSg/PdfParser

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 1.0 Universal license. 

To view a copy of this license, read the file CopyRight.md or visit 
http://creativecommons.org/publicdomain/zero/1.0

This software is distributed without any warranty. 
**************************************************************************************/

using System.Collections.Generic;
using System.IO;
using System.Text;


//https://blog.idrsolutions.com/2013/01/understanding-the-pdf-file-format-overview/#pdf-fonts

namespace PdfParserLib {


  public class PdfParser {


    #region Properties
    //      ----------
    
    /// <summary>
    /// Path and file name if an actual file and not just bytes get parsed.
    /// </summary>
    public readonly string? PathFileName;


    /// <summary>
    /// Version from pdf file header
    /// </summary>
    public string PdfVersion { get { return Tokeniser.PdfVersion; } }


    /// <summary>
    /// Some information provided by the pdf writer about the file
    /// </summary>
    public string? DocumentInfo { get { return Tokeniser.DocumentInfo; } }


    /// <summary>
    /// Unique identifier for pdf file. It has 2 parts. The first identifies the file, the second identifies the version.
    /// </summary>
    public string? DocumentID { get { return Tokeniser.DocumentID; } }


    /// <summary>
    /// List of all pages in the pdf file. Iterate through every page to get the text content of the file.
    /// </summary>
    public IReadOnlyList<PdfPage> Pages { get { return Tokeniser.Pages; } }


    /// <summary>
    /// Tokeniser used to break down the pdf bytes into pdf tokens
    /// </summary>
    public Tokeniser Tokeniser { get { return tokeniser; } }
    Tokeniser tokeniser;
    #endregion


    #region Constructors
    //      ------------

    public PdfParser(
      string pathFileName,
      string password = "",
      string contentDelimiter = "|", 
      byte[]? streamBuffer = null, 
      StringBuilder? stringBuilder = null) : 
        this(File.ReadAllBytes(pathFileName), password, contentDelimiter, streamBuffer, stringBuilder) 
    {
      PathFileName = pathFileName;
    }


    public PdfParser(
      byte[] pdfBytes,
      string password = "",
      string contentDelimiter = "|", 
      byte[]? streamBuffer = null, 
      StringBuilder? stringBuilder = null) 
    {

      tokeniser = new Tokeniser(pdfBytes, password, contentDelimiter, streamBuffer, stringBuilder);
      tokeniser.VerifyFileHeader();
      tokeniser.FindPages();
    }
    #endregion


    #region Methods
    //      -------



    #endregion
  }
}
