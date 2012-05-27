using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

public class Helpers
{
    public static string Combine(string strParent, string strItem)
    {
        return Path.Combine(strParent, strItem);
    }

    public static void EnsureDirectory(string strPath)
    {
        if (!Directory.Exists(strPath))
        {
            Directory.CreateDirectory(strPath);
        }
    }

    // replace illegal characters from a filename with a sutiable substitute

    public static string CleanFilename(string str)
    {
        return CleanFilename(str, str.Length);
    }

    public static string CleanFilename(string str, int cch)
    {
        char[] aToEscape = str.ToCharArray(0, Math.Min(cch, str.Length));

        for (int i = 0; i < aToEscape.Length; i++)
        {
            switch (aToEscape[i])
            {
                case ':':
                    aToEscape[i] = ';';
                    break;

                case '’':
                case '\"':
                    aToEscape[i] = '\'';
                    break;

                case '<':
                    aToEscape[i] = '(';
                    break;

                case '>':
                    aToEscape[i] = ')';
                    break;

                default:
                    String strInvalid = "\\/*?|";
                    if (-1 != strInvalid.IndexOf(aToEscape[i]))
                    {
                        aToEscape[i] = '_';
                    }
                    break;
            }
        }

        return new String(aToEscape);
    }
}
