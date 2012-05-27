// Lightweight Property List (.plist) parser
// Copyright (c)2012 David De Vorchik
//
// Parse the entire property list into a directory of objects. We use the SAX parser to incrementally read the file vs. the DOM.  
// This avoids the creation of the DOM structure that duplicates the file contents.
// 
// 042411 - small refectoring, exceptions thrown on parse errors
// 042411 - adding a comment
// 043011 - fixed empty dictionary parsing and simplified token fetching and value assingments
// 050611 - added accessors with default values
// 051611 - disabled DTD validation to allow offline parsing
// 080611 - removed various accessors in favor of using "dynamic"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Xml;
using System.Xml.Schema;

public class PropertyList
{
    public PropertyList()
    {
        _dictValues = new Dictionary<string, dynamic>();
    }

    public dynamic Value(string strKey)
    {
        return _dictValues[strKey];
    }

    public dynamic Value(string strKey, dynamic defValue)
    {
        return Contains(strKey) ? _dictValues[strKey] : defValue;
    }

    public bool Contains(string strKey)
    {
        return _dictValues.ContainsKey(strKey);
    }

    private Dictionary<string, dynamic> _dictValues;

    private bool _GetToken(XmlReader reader, out string strToken)
    {
        bool fResult = reader.IsStartElement();
        strToken = fResult ? reader.LocalName : null;
        return fResult;
    }

    private bool _NextToken(XmlReader reader)
    {
        while (!reader.EOF && reader.Read() && !reader.IsStartElement())
            ;

        return !reader.EOF;
    }

    private dynamic _ParseValue(XmlReader reader)
    {
        string strToken;

        if (_GetToken(reader, out strToken))
        {
            switch (strToken.ToLower())
            {
                case "true":
                    return true;

                case "false":
                    return false;

                case "string":
                    return reader.ReadString();

                case "date":
                    return DateTime.Parse(reader.ReadString());

                case "integer":
                    return Int64.Parse(reader.ReadString());

                case "dict":
                {
                    XmlReader sub = reader.ReadSubtree();
                    sub.MoveToContent();

                    PropertyList dict = new PropertyList();
                    dict._ParseDictionary(sub);

                    return dict;
                }

                case "array":
                {
                    XmlReader sub = reader.ReadSubtree();
                    sub.MoveToContent();

                    List<PropertyList> list = new List<PropertyList>();
                    while (_NextToken(sub))
                    {
                        string strSubToken;
                        if (_GetToken(reader, out strSubToken) && strSubToken.ToLower() == "dict")
                        {
                            XmlReader subDict = reader.ReadSubtree();
                            subDict.MoveToContent();

                            PropertyList dict = new PropertyList();
                            dict._ParseDictionary(subDict);
                            list.Add(dict);
                        }
                        else
                        {
                            throw new FormatException("Invalid array -- we only support dictionaries");
                        }
                    }

                    return list;
                }

                case "data":
// TODO: we silently eat "data" elements, eventaully translate these into a object
                    break;  

                default:
                {
                    throw new FormatException("Invalid type: " + reader.LocalName.ToLower());
                }
            }
        }

        return null;        
    }

    private void _ParseDictionary(XmlReader reader)
    {
        string strToken;
        if (_GetToken(reader, out strToken) && strToken == "dict")
        {
            _NextToken(reader);
            while (_GetToken(reader, out strToken))
            {
                if (strToken == "key")
                {
                    string strKey = reader.ReadString();
                    if (_NextToken(reader))
                    {
                        _dictValues.Add(strKey, _ParseValue(reader));
                    }
                    else
                    {
                        throw new FormatException("Missing value for key: " + strKey);
                    }

                    _NextToken(reader);
                }
                else
                {
                    throw new FormatException("Missing key");
                }
            }
        }
        else
        {
            throw new FormatException("Badly formed dictionary");
        }
    }        

    private void _ParseFromReader(XmlReader reader)
    {
        reader.MoveToContent();

        string strToken;
        if (_GetToken(reader, out strToken) && strToken == "plist")
        {
            if (_NextToken(reader))
            {
                _ParseDictionary(reader);
            }
        }
        else
        {
            throw new FormatException("Badly format PLIST -- missing opening element");
        }
    }

    public void ParseFile(string strFilename)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Ignore;              // don't validate DTD files, if we do then offline parsing won't work

        XmlReader reader = XmlReader.Create(strFilename, settings);
        _ParseFromReader(reader);
    }   
}
