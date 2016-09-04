using System;
using System.Globalization;
using System.Collections;
using iTextSharp.text.xml;
using System.util;

namespace iTextSharp.text.html
{

    /// <summary>
    /// This interface is implemented by the peer of all the iText objects.
    /// </summary>

    public class HtmlPeer : XmlPeer
    {

        /// <summary>
        /// Creates a XmlPeer.
        /// </summary>
        /// <param name="name">the iText name of the tag</param>
        /// <param name="alias">the Html name of the tag</param>
        public HtmlPeer(string name, string alias) : base(name, alias.ToLower(CultureInfo.InvariantCulture))
        {
        }

        /// <summary>
        /// Sets an alias for an attribute.
        /// </summary>
        /// <param name="name">the iText tagname</param>
        /// <param name="alias">the custom tagname</param>
        public override void AddAlias(string name, string alias)
        {
            AttributeAliases.Add(alias.ToLower(CultureInfo.InvariantCulture), name);
        }

        /// <summary>
        /// @see com.lowagie.text.xml.XmlPeer#getAttributes(org.xml.sax.Attributes)
        /// </summary>
        public override Properties GetAttributes(Hashtable attrs)
        {
            Properties attributes = new Properties();
            attributes.AddAll(AttributeValues);
            if (DefaultContent != null)
            {
                attributes[ElementTags.ITEXT] = DefaultContent;
            }
            if (attrs != null)
            {
                foreach (string key in attrs.Keys)
                {
                    attributes.Add(GetName(key).ToLower(CultureInfo.InvariantCulture), (string)attrs[key]);
                }
            }
            return attributes;
        }
    }
}