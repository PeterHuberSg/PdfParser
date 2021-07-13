/**************************************************************************************

ExceptionExtension
==================

Provides extention methods for Exception 

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
using System.Reflection;
using System.Text;


namespace PdfParserLib {

	/// <summary>
	/// Holds extension method to display details of an exception
	/// </summary>
	public static class ExceptionExtension {

		#region Methods
		//      -------

		/// <summary>
		/// Lists all details of any exception type (not just PBoxExceptions) into
		/// a string
		/// </summary>
		static public string ToDetailString(this Exception thisException) {
			StringBuilder exceptionInfo = new StringBuilder();
			int startPos;
			int titelLength;

			// Loop through all exceptions
			Exception? currentException = thisException;  // Temp variable to hold InnerException object during the loop.
			int exceptionCount = 1;       // Count variable to track the number of exceptions in the chain.
			do {
				// exception type and message as title
				startPos = exceptionInfo.Length;
				exceptionInfo.Append(currentException.GetType().FullName);
				titelLength = exceptionInfo.Length - startPos;
				exceptionInfo.Append("\r\n");
				if (exceptionCount==1) {
					//main exception
					exceptionInfo.Append('=', titelLength);
				} else {
					//inner exceptions
					exceptionInfo.Append('-', titelLength);
				}

				exceptionInfo.Append("\r\n" + currentException.Message);
				// List the remaining properties of all other exceptions
				PropertyInfo[] propertiesArray = currentException.GetType().GetProperties();
				foreach (PropertyInfo property in propertiesArray) {
					// skip message, inner exception and stack trace
					if (property.Name != "InnerException" && property.Name != "StackTrace" && property.Name != "Message" && property.Name != "TargetSite") {
						if (property.GetValue(currentException, null) == null) {
							//skip empty properties
						} else {
							exceptionInfo.AppendFormat("\r\n" + property.Name + ": " + property.GetValue(currentException, null));
						}
					}
				}

				// record the StackTrace with separate label.
				if (currentException.StackTrace != null) {
					exceptionInfo.Append("\r\n" + currentException.StackTrace + "\r\n");
				}
				exceptionInfo.Append("\r\n");

				// continue with inner exception
				currentException = currentException.InnerException;
				exceptionCount++;
			} while (currentException!=null);

			return exceptionInfo.ToString();
		}
		#endregion

	}
}