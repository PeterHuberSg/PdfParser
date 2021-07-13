# PdfParser

**Table of content**


## Introduction
The goal of this project is to extract some text from pdf files without human 
intervention and to use the extracted text for further processing. I managed to 
extract the financial figures from my pdf bank statement using only .NET 
and no other library within 3 days. I thought it would be no big deal to parse the
pdf file properly according to the pdf specification. What I expected to take a week or 
so took several months. Here you find the fruits of this labor:

* The ***PdfParser.dll***, which parses a pdf file, analysis its content, goes through all 
pages and extracts the text it finds.

* The ***PdfFilesTextBrowser.exe***, a WPF application which lets you browse through
pdf files on your PC and displays:
  1. Every page of the document and it's text
  1. The structure of the pdf file like header, xref table, page object, fonts, ...
  1. The raw content of the pdf file. It gives a compact overview over the possibly 
    megabytes of data. It highlights the different objects in the pdf file and allows 
    easy navigation among them.
  1. Adobe Acrobat Reader displaying the pdf file

Why was this massive programming effort necessary ? Unfortunately is the pdf 
specification based on technology from the precious century and it shows. It's mostly
text which leaves a lot open to interpretation. Even worse is that many pdf writers 
violet this specification, but Adobe Acrobat Reader manages to display the document 
anyway properly. Meaning a good pdf extractor has to handle also pdf files which 
don't follow the specification.

Another reason for the PdfFilesTextBrowser is that pdf does not care about blanks, words, 
paragraphs and so on. It only cares about painting pixels to a screen or on a paper, 
which it does symbol by symbol. These symbols might be characters, but the pdf writer 
can create its own font and add it to the file. Those fonts don't need to follow any 
coding convention like for example Unicode. Sometimes it is simply not possible to 
translate such a symbol to a character. To understand better what is going on, it really
helps to see in the left part of the PdfFilesTextBrowser the extracted text and on
the right side how Adobe Acrobat Reader displays it.

Even if the characters can be recognised, the resulting text might look rather strange.
The reason is that Adobe doesn't need blanks not end of line characters. It can place
every single on its own precisely. A new word doesn't need a blank, the first letter
just gets moved a bit further to the right. End of line characters are not used at all.
However, pdf groups text content together by the font used. Since a word can consist of
more than one font, in the text extraction it might look as if the word is distributed 
over 2 paragraphs.

**But fear not** If you are interested in extracting text from the same type of pdf file, 
like monthly bank statement, it is quite doable to analyse the extracted text and find
the required data.




