﻿using System;
using System.Collections.Generic;

namespace HTMLParser {
    public static class HTML {
        public static List<DOMElement> Parse(string source) {
            source = Utils.ClearBreakingCharacters(source);

            int parsingFromSourceTime = 0;

            List<DOMElement> tagsList = GetTagsList(source, ref parsingFromSourceTime);
            List<DOMElement> tree = GetDOMTree(tagsList, parsingFromSourceTime);

            return tree;
        }

        /// <summary>
        /// Parses source code into objects
        /// </summary>
        public static List<DOMElement> GetTagsList(string source, ref int parsingTime) {
            List<DOMElement> tags = new List<DOMElement>();

            DateTime timeBeforeParsing = DateTime.Now;

            bool addNewElements = true;
            int startParsingIndex = -1;

            for (int i = 0; i < source.Length; i++) {
                char character = source[i];

                if (character == '<') {
                    // Get tag's end index (>)
                    int tagEndIndex = Utils.SearchForClosestChar(source, '>', i + 1);
                    // Get tag's code
                    string tagCode = TagUtils.GetCode(source, i, tagEndIndex);
                    // Get tag's name
                    string tagName = TagUtils.GetName(tagCode);
                    // Get tag's type
                    TagType tagType = TagUtils.GetType(tagCode);
                    // Create DOMElement
                    DOMElement element = new DOMElement() {
                        TagCode = tagCode,
                        TagName = tagName,
                        Type = tagType,
                        TagStartIndex = i,
                        TagEndIndex = tagEndIndex
                    };

                    if (element.Type == TagType.Opening) { 
                        for (int l = 0; l < HTMLSelfClosingTags.list.Count; l++) {
                            if (element.TagName == HTMLSelfClosingTags.list[l]) {
                                element.Type = TagType.SelfClosing;
                                break;
                            }
                        }
                    }

                    if (element.Type == TagType.Opening || element.Type == TagType.SelfClosing) {
                        element.Attributes = GetAttributes(element);
                    }

                    // Tags that are disabling parsing for until they are closed
                    if (element.TagName == "script" || element.TagName == "style") {
                        if (element.Type == TagType.Opening && addNewElements) {
                            addNewElements = false;
                            tags.Add(element);
                            
                            startParsingIndex = GetIndexOfLastTag(source, element.TagEndIndex + 1, element.TagName);
                        } else if (element.TagEndIndex == startParsingIndex && !addNewElements) {
                            addNewElements = true;
                            startParsingIndex = -1;
                        }
                    }

                    // Get text between tags
                    if (tags.Count > 0 && addNewElements) {
                        DOMElement last = tags[tags.Count - 1];

                        int startIndex = last.TagEndIndex + 1;
                        int endIndex = element.TagStartIndex - last.TagEndIndex - 1;

                        string content = source.Substring(startIndex, endIndex).Trim();

                        if (content.Length > 0) {
                            DOMElement text = new DOMElement() {
                                Type = TagType.Text,
                                Content = content
                            };

                            if (last.Type == TagType.SelfClosing) {
                                last = tags[tags.Count - 2];
                            }

                            last.Children.Add(text);
                        }
                    }

                    // Add it to the list
                    if (addNewElements) tags.Add(element);
                }
            }

            DateTime timeAfterParsing = DateTime.Now;

            parsingTime = timeAfterParsing.Millisecond - timeBeforeParsing.Millisecond;

            return tags;
        }

        /// <summary>
        /// Creates DOM tree from tags list
        /// </summary>
        public static List<DOMElement> GetDOMTree(List<DOMElement> tagsList, int parsingFromSourceTime = -1) {
            List<DOMElement> tree = new List<DOMElement>();
            List<DOMElement> parentsList = new List<DOMElement>();

            int openedTags = 0;

            DateTime timeBeforeTreeParsing = DateTime.Now;

            for (int i = 0; i < tagsList.Count; i++) {
                DOMElement element = tagsList[i];
                DOMElement lastParent = parentsList.Count > 0 ? parentsList[parentsList.Count - 1] : null;

                // Add tag that isn't any tag's child
                // For example <html> is first tag in a document so it isn't any tag's child
                if (parentsList.Count == 0 || (element.Type == TagType.Closing && parentsList.Count == 1)) {
                    tree.Add(element);
                    // Add tag as a parent
                    // For example <html>
                    if (element.Type == TagType.Opening) {
                        parentsList.Add(element);
                    }
                }
                // For every closing tag
                // remove last parent from parentsList
                // add closing tag as DOMElement to tree
                else if (element.Type == TagType.Closing) {
                    if (element.TagName == lastParent.TagName) {
                        parentsList.Remove(lastParent);
                        parentsList[parentsList.Count - 1].Children.Add(element);
                    } else {
                        DOMElement closingTag = new DOMElement() {
                            Type = TagType.Closing,
                            TagCode = "/" + lastParent.TagName,
                            TagName = lastParent.TagName
                        };

                        parentsList.Remove(lastParent);
                        parentsList[parentsList.Count - 1].Children.Add(closingTag);

                        i--;
                    }

                    openedTags--;
                }
                // For every opening tag
                // add it as last parent's child
                // and select it as a new parent
                else if (element.Type == TagType.Opening) {
                    // Add the child to last parent
                    lastParent.Children.Add(element);
                    // Add new parent
                    parentsList.Add(element);
                    openedTags++;
                }
                else if (element.Type == TagType.SelfClosing) {
                    lastParent.Children.Add(element);
                }
            }

            DateTime timeAfterTreeParsing = DateTime.Now;

            // Write DOM tree into console
            Console.WriteLine();
            Utils.WriteDOMTree(tree);

            // Opened tags
            Utils.Log("\n\nDocument is: ",
                (openedTags > 0 ? "Invalid" : "Valid") + " (" + openedTags + ")",
                openedTags > 0 ? ConsoleColor.Red : ConsoleColor.Green
            );

            if (parsingFromSourceTime != -1) {
                // DOM tree parsing time in ms
                Utils.Log("\nTime of parsing from source code into tags list: ",
                    parsingFromSourceTime + "ms"
                );
            }

            // DOM tree parsing time in ms
            Utils.Log("\nTime of parsing into DOM tree : ",
                (timeAfterTreeParsing.Millisecond - timeBeforeTreeParsing.Millisecond).ToString() + "ms"
            );

            return tree;
        }

        private static int GetIndexOfLastTag (string source, int startIndex, string tagName) {
            List<int> indexes = new List<int>();

            for (int i = startIndex; i < source.Length; i++) {   
                if (source[i] == '<') {
                    int index = Utils.SearchForClosestChar(source, '>', i);
                    string code = TagUtils.GetCode(source, i, index);
                    string name = TagUtils.GetName(code);                    

                    if (name == tagName && code.StartsWith("/")) {
                        indexes.Add(index);
                    }
                }
            }

            return indexes.Count > 0 ? indexes[indexes.Count - 1] : -1;
        }

        /// <summary>
        /// Parses tag's code into attributes
        /// </summary>
        public static List<DOMElementAttribute> GetAttributes(DOMElement element) {
            string tagCode = element.TagCode.Substring(element.TagName.Length, element.TagCode.Length - element.TagName.Length).Trim();

            List<DOMElementAttribute> attributesList = new List<DOMElementAttribute>();

            if (tagCode.Length > 0) {
                bool searchForAttribute = true;
                bool searchForValue = false;
                int openedQuotes = 0;

                char quoteChar = '"';

                for (int i = 0; i < tagCode.Length; i++) {
                    DOMElementAttribute lastAttribute = attributesList.Count > 0 ? attributesList[attributesList.Count - 1] : null;

                    if (tagCode[i] != ' ') {
                        if (searchForAttribute) {
                            DOMElementAttribute attribute = new DOMElementAttribute();
                            // Set attribute's property start index as the loop index
                            attribute.PropertyStartIndex = i;

                            // Get closest equals char index for getting attribute property
                            int closestEqualsChar = Utils.SearchForClosestChar(tagCode, '=', i);
                            // If there isn't any equals char then use tag's content length as index
                            if (closestEqualsChar == -1) closestEqualsChar = tagCode.Length;
                            // Get content betweeen tag's property start index and closest equals char
                            string content = tagCode.Substring(i, closestEqualsChar - i);
                            // Check if in content is any space
                            int spaceIndexInContent = Utils.SearchForClosestChar(content, ' ', 0);
                            // If attribute has value, there isn't any spaces in content
                            bool hasValue = spaceIndexInContent == -1;

                            // Get attribute's property end index
                            attribute.PropertyEndIndex = spaceIndexInContent == -1 ? closestEqualsChar : (spaceIndexInContent + attribute.PropertyStartIndex);
                            // Get property by cutting
                            attribute.Property = tagCode.Substring(attribute.PropertyStartIndex, attribute.PropertyEndIndex - attribute.PropertyStartIndex);
                            // Add attribute to the list
                            attributesList.Add(attribute);

                            // If attribute has value, then its searching for value
                            if (hasValue) {
                                searchForValue = true;
                            } else {
                                // Skip the loop index
                                i = attribute.PropertyEndIndex;
                            }

                            // Search for another attribute
                            searchForAttribute = !hasValue;
                        }
                        // Search for value
                        // Count opened quotes (" and ')
                        else if (searchForValue) {
                            // If quote char is an apostrophe
                            if (openedQuotes == 0 && tagCode[i] == '\'') quoteChar = '\'';

                            if (tagCode[i] == quoteChar) {
                                // Open value
                                if (openedQuotes == 0) {
                                    lastAttribute.ValueStartIndex = i + 1;
                                    openedQuotes++;
                                }
                                // Close value
                                else if (openedQuotes == 1) {
                                    // Get attribute's value
                                    lastAttribute.ValueEndIndex = i;
                                    lastAttribute.Value = tagCode.Substring(lastAttribute.ValueStartIndex, lastAttribute.ValueEndIndex - lastAttribute.ValueStartIndex);

                                    // Reset parser
                                    openedQuotes = 0;
                                    searchForValue = false;
                                    searchForAttribute = true;
                                    quoteChar = '"';
                                }
                            }
                        }
                    }
                }
            }

            return attributesList;
        }
    }
}
