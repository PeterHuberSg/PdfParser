using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;


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
