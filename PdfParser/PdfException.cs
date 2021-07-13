/**************************************************************************************

PdfException
============

Specialised Exception used by Tokeniser to show an error and the part of the pdf file 
where it accured

Written in 2021 by Jürgpeter Huber, Singapore

Contact: https://github.com/PeterHuberSg/PdfParser

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 1.0 Universal license. 

To view a copy of this license, read the file CopyRight.md or visit 
http://creativecommons.org/publicdomain/zero/1.0

This software is distributed without any warranty. 
**************************************************************************************/

using System;


namespace PdfParserLib {


  public class PdfException: Exception {

    public readonly Tokeniser Tokeniser;


    /// <summary>
    /// Shows with the exception message also the buffer content where the exception was thrown
    /// </summary>
    public PdfException(string message, Tokeniser tokeniser) : base(tokeniser.PdfExceptionMessage(message)) {
      Tokeniser = tokeniser;
    }


    /// <summary>
    /// Shows with the exception message and the innerException also the buffer content where the exception was thrown
    /// </summary>
    public PdfException(string message, Exception innerException, Tokeniser tokeniser) : 
      base(tokeniser.PdfExceptionMessage(message), innerException) 
    {
      Tokeniser = tokeniser;
    }
  }


  public class PdfStreamException: Exception {

    public readonly Tokeniser Tokeniser;


    /// <summary>
    /// Shows with the exception message also the stream content where the exception was thrown
    /// </summary>
    public PdfStreamException(string message, Tokeniser tokeniser) : base(tokeniser.PdfStreamExceptionMessage(message)) {
      Tokeniser = tokeniser;
    }


    /// <summary>
    /// Shows with the exception message and the innerException also the stream content where the exception was thrown
    /// </summary>
    public PdfStreamException(string message, Exception innerException, Tokeniser tokeniser) :
      base(tokeniser.PdfStreamExceptionMessage(message), innerException) 
    {
      Tokeniser = tokeniser;
    }
  }

}
