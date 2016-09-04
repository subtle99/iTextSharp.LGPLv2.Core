using System;
using System.Collections;
using iTextSharp.LGPLv2.Core.System.Encodings;

namespace iTextSharp.text.pdf
{
    /// <summary>
    /// Subsets a True Type font by removing the unneeded glyphs from
    /// the font.
    /// @author  Paulo Soares (psoares@consiste.pt)
    /// </summary>
    internal class TrueTypeFontSubSet
    {
        internal static readonly int Arg1And2AreWords = 1;

        internal static readonly int[] EntrySelectors = { 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4 };

        internal static readonly int HeadLocaFormatOffset = 51;

        internal static readonly int MoreComponents = 32;

        internal static readonly int TableChecksum = 0;

        internal static readonly int TableLength = 2;

        internal static readonly string[] TableNamesCmap = {"cmap", "cvt ", "fpgm", "glyf", "head",
                                             "hhea", "hmtx", "loca", "maxp", "prep"};

        internal static readonly string[] TableNamesExtra = {"OS/2", "cmap", "cvt ", "fpgm", "glyf", "head",
            "hhea", "hmtx", "loca", "maxp", "name, prep"};

        internal static readonly string[] TableNamesSimple = {"cvt ", "fpgm", "glyf", "head",
                                               "hhea", "hmtx", "loca", "maxp", "prep"};
        internal static readonly int TableOffset = 1;
        internal static readonly int WeHaveAnXAndYScale = 64;
        internal static readonly int WeHaveAScale = 8;
        internal static readonly int WeHaveATwoByTwo = 128;


        protected int DirectoryOffset;

        /// <summary>
        /// The file name.
        /// </summary>
        protected string FileName;

        protected int FontPtr;

        protected int GlyfTableRealSize;

        protected ArrayList GlyphsInList;

        protected Hashtable GlyphsUsed;

        protected bool IncludeCmap;

        protected bool IncludeExtras;

        protected bool LocaShortTable;

        protected int[] LocaTable;

        protected int LocaTableRealSize;

        protected byte[] NewGlyfTable;

        protected int[] NewLocaTable;

        protected byte[] NewLocaTableOut;

        protected byte[] OutFont;

        /// <summary>
        /// The file in use.
        /// </summary>
        protected RandomAccessFileOrArray Rf;

        /// <summary>
        /// Contains the location of the several tables. The key is the name of
        /// the table and the value is an  int[3]  where position 0
        /// is the checksum, position 1 is the offset from the start of the file
        /// and position 2 is the length of the table.
        /// </summary>
        protected Hashtable TableDirectory;
        protected int TableGlyphOffset;

        /// <summary>
        /// Creates a new TrueTypeFontSubSet
        /// </summary>
        /// <param name="directoryOffset">The offset from the start of the file to the table directory</param>
        /// <param name="fileName">the file name of the font</param>
        /// <param name="rf"></param>
        /// <param name="glyphsUsed">the glyphs used</param>
        /// <param name="includeCmap"> true  if the table cmap is to be included in the generated font</param>
        /// <param name="includeExtras"></param>
        internal TrueTypeFontSubSet(string fileName, RandomAccessFileOrArray rf, Hashtable glyphsUsed, int directoryOffset, bool includeCmap, bool includeExtras)
        {
            FileName = fileName;
            Rf = rf;
            GlyphsUsed = glyphsUsed;
            IncludeCmap = includeCmap;
            IncludeExtras = includeExtras;
            DirectoryOffset = directoryOffset;
            GlyphsInList = new ArrayList(glyphsUsed.Keys);
        }

        /// <summary>
        /// Does the actual work of subsetting the font.
        /// @throws IOException on error
        /// @throws DocumentException on error
        /// </summary>
        /// <returns>the subset font</returns>
        internal byte[] Process()
        {
            try
            {
                Rf.ReOpen();
                CreateTableDirectory();
                ReadLoca();
                FlatGlyphs();
                CreateNewGlyphTables();
                LocaTobytes();
                AssembleFont();
                return OutFont;
            }
            finally
            {
                try
                {
                    Rf.Close();
                }
                catch
                {
                    // empty on purpose
                }
            }
        }

        protected void AssembleFont()
        {
            int[] tableLocation;
            int fullFontSize = 0;
            string[] tableNames;
            if (IncludeExtras)
                tableNames = TableNamesExtra;
            else
            {
                if (IncludeCmap)
                    tableNames = TableNamesCmap;
                else
                    tableNames = TableNamesSimple;
            }
            int tablesUsed = 2;
            int len = 0;
            for (int k = 0; k < tableNames.Length; ++k)
            {
                string name = tableNames[k];
                if (name.Equals("glyf") || name.Equals("loca"))
                    continue;
                tableLocation = (int[])TableDirectory[name];
                if (tableLocation == null)
                    continue;
                ++tablesUsed;
                fullFontSize += (tableLocation[TableLength] + 3) & (~3);
            }
            fullFontSize += NewLocaTableOut.Length;
            fullFontSize += NewGlyfTable.Length;
            int iref = 16 * tablesUsed + 12;
            fullFontSize += iref;
            OutFont = new byte[fullFontSize];
            FontPtr = 0;
            WriteFontInt(0x00010000);
            WriteFontShort(tablesUsed);
            int selector = EntrySelectors[tablesUsed];
            WriteFontShort((1 << selector) * 16);
            WriteFontShort(selector);
            WriteFontShort((tablesUsed - (1 << selector)) * 16);
            for (int k = 0; k < tableNames.Length; ++k)
            {
                string name = tableNames[k];
                tableLocation = (int[])TableDirectory[name];
                if (tableLocation == null)
                    continue;
                WriteFontString(name);
                if (name.Equals("glyf"))
                {
                    WriteFontInt(CalculateChecksum(NewGlyfTable));
                    len = GlyfTableRealSize;
                }
                else if (name.Equals("loca"))
                {
                    WriteFontInt(CalculateChecksum(NewLocaTableOut));
                    len = LocaTableRealSize;
                }
                else
                {
                    WriteFontInt(tableLocation[TableChecksum]);
                    len = tableLocation[TableLength];
                }
                WriteFontInt(iref);
                WriteFontInt(len);
                iref += (len + 3) & (~3);
            }
            for (int k = 0; k < tableNames.Length; ++k)
            {
                string name = tableNames[k];
                tableLocation = (int[])TableDirectory[name];
                if (tableLocation == null)
                    continue;
                if (name.Equals("glyf"))
                {
                    Array.Copy(NewGlyfTable, 0, OutFont, FontPtr, NewGlyfTable.Length);
                    FontPtr += NewGlyfTable.Length;
                    NewGlyfTable = null;
                }
                else if (name.Equals("loca"))
                {
                    Array.Copy(NewLocaTableOut, 0, OutFont, FontPtr, NewLocaTableOut.Length);
                    FontPtr += NewLocaTableOut.Length;
                    NewLocaTableOut = null;
                }
                else
                {
                    Rf.Seek(tableLocation[TableOffset]);
                    Rf.ReadFully(OutFont, FontPtr, tableLocation[TableLength]);
                    FontPtr += (tableLocation[TableLength] + 3) & (~3);
                }
            }
        }

        protected int CalculateChecksum(byte[] b)
        {
            int len = b.Length / 4;
            int v0 = 0;
            int v1 = 0;
            int v2 = 0;
            int v3 = 0;
            int ptr = 0;
            for (int k = 0; k < len; ++k)
            {
                v3 += b[ptr++] & 0xff;
                v2 += b[ptr++] & 0xff;
                v1 += b[ptr++] & 0xff;
                v0 += b[ptr++] & 0xff;
            }
            return v0 + (v1 << 8) + (v2 << 16) + (v3 << 24);
        }

        protected void CheckGlyphComposite(int glyph)
        {
            int start = LocaTable[glyph];
            if (start == LocaTable[glyph + 1]) // no contour
                return;
            Rf.Seek(TableGlyphOffset + start);
            int numContours = Rf.ReadShort();
            if (numContours >= 0)
                return;
            Rf.SkipBytes(8);
            for (;;)
            {
                int flags = Rf.ReadUnsignedShort();
                int cGlyph = Rf.ReadUnsignedShort();
                if (!GlyphsUsed.ContainsKey(cGlyph))
                {
                    GlyphsUsed[cGlyph] = null;
                    GlyphsInList.Add(cGlyph);
                }
                if ((flags & MoreComponents) == 0)
                    return;
                int skip;
                if ((flags & Arg1And2AreWords) != 0)
                    skip = 4;
                else
                    skip = 2;
                if ((flags & WeHaveAScale) != 0)
                    skip += 2;
                else if ((flags & WeHaveAnXAndYScale) != 0)
                    skip += 4;
                if ((flags & WeHaveATwoByTwo) != 0)
                    skip += 8;
                Rf.SkipBytes(skip);
            }
        }

        protected void CreateNewGlyphTables()
        {
            NewLocaTable = new int[LocaTable.Length];
            int[] activeGlyphs = new int[GlyphsInList.Count];
            for (int k = 0; k < activeGlyphs.Length; ++k)
                activeGlyphs[k] = (int)GlyphsInList[k];
            Array.Sort(activeGlyphs);
            int glyfSize = 0;
            for (int k = 0; k < activeGlyphs.Length; ++k)
            {
                int glyph = activeGlyphs[k];
                glyfSize += LocaTable[glyph + 1] - LocaTable[glyph];
            }
            GlyfTableRealSize = glyfSize;
            glyfSize = (glyfSize + 3) & (~3);
            NewGlyfTable = new byte[glyfSize];
            int glyfPtr = 0;
            int listGlyf = 0;
            for (int k = 0; k < NewLocaTable.Length; ++k)
            {
                NewLocaTable[k] = glyfPtr;
                if (listGlyf < activeGlyphs.Length && activeGlyphs[listGlyf] == k)
                {
                    ++listGlyf;
                    NewLocaTable[k] = glyfPtr;
                    int start = LocaTable[k];
                    int len = LocaTable[k + 1] - start;
                    if (len > 0)
                    {
                        Rf.Seek(TableGlyphOffset + start);
                        Rf.ReadFully(NewGlyfTable, glyfPtr, len);
                        glyfPtr += len;
                    }
                }
            }
        }

        protected void CreateTableDirectory()
        {
            TableDirectory = new Hashtable();
            Rf.Seek(DirectoryOffset);
            int id = Rf.ReadInt();
            if (id != 0x00010000)
                throw new DocumentException(FileName + " is not a true type file.");
            int numTables = Rf.ReadUnsignedShort();
            Rf.SkipBytes(6);
            for (int k = 0; k < numTables; ++k)
            {
                string tag = ReadStandardString(4);
                int[] tableLocation = new int[3];
                tableLocation[TableChecksum] = Rf.ReadInt();
                tableLocation[TableOffset] = Rf.ReadInt();
                tableLocation[TableLength] = Rf.ReadInt();
                TableDirectory[tag] = tableLocation;
            }
        }

        protected void FlatGlyphs()
        {
            int[] tableLocation;
            tableLocation = (int[])TableDirectory["glyf"];
            if (tableLocation == null)
                throw new DocumentException("Table 'glyf' does not exist in " + FileName);
            int glyph0 = 0;
            if (!GlyphsUsed.ContainsKey(glyph0))
            {
                GlyphsUsed[glyph0] = null;
                GlyphsInList.Add(glyph0);
            }
            TableGlyphOffset = tableLocation[TableOffset];
            for (int k = 0; k < GlyphsInList.Count; ++k)
            {
                int glyph = (int)GlyphsInList[k];
                CheckGlyphComposite(glyph);
            }
        }

        protected void LocaTobytes()
        {
            if (LocaShortTable)
                LocaTableRealSize = NewLocaTable.Length * 2;
            else
                LocaTableRealSize = NewLocaTable.Length * 4;
            NewLocaTableOut = new byte[(LocaTableRealSize + 3) & (~3)];
            OutFont = NewLocaTableOut;
            FontPtr = 0;
            for (int k = 0; k < NewLocaTable.Length; ++k)
            {
                if (LocaShortTable)
                    WriteFontShort(NewLocaTable[k] / 2);
                else
                    WriteFontInt(NewLocaTable[k]);
            }

        }

        protected void ReadLoca()
        {
            int[] tableLocation;
            tableLocation = (int[])TableDirectory["head"];
            if (tableLocation == null)
                throw new DocumentException("Table 'head' does not exist in " + FileName);
            Rf.Seek(tableLocation[TableOffset] + HeadLocaFormatOffset);
            LocaShortTable = (Rf.ReadUnsignedShort() == 0);
            tableLocation = (int[])TableDirectory["loca"];
            if (tableLocation == null)
                throw new DocumentException("Table 'loca' does not exist in " + FileName);
            Rf.Seek(tableLocation[TableOffset]);
            if (LocaShortTable)
            {
                int entries = tableLocation[TableLength] / 2;
                LocaTable = new int[entries];
                for (int k = 0; k < entries; ++k)
                    LocaTable[k] = Rf.ReadUnsignedShort() * 2;
            }
            else
            {
                int entries = tableLocation[TableLength] / 4;
                LocaTable = new int[entries];
                for (int k = 0; k < entries; ++k)
                    LocaTable[k] = Rf.ReadInt();
            }
        }

        /// <summary>
        /// Reads a  string  from the font file as bytes using the Cp1252
        /// encoding.
        /// @throws IOException the font file could not be read
        /// </summary>
        /// <param name="length">the length of bytes to read</param>
        /// <returns>the  string  read</returns>
        protected string ReadStandardString(int length)
        {
            byte[] buf = new byte[length];
            Rf.ReadFully(buf);
            return EncodingsRegistry.Instance.GetEncoding(1252).GetString(buf);
        }

        protected void WriteFontInt(int n)
        {
            OutFont[FontPtr++] = (byte)(n >> 24);
            OutFont[FontPtr++] = (byte)(n >> 16);
            OutFont[FontPtr++] = (byte)(n >> 8);
            OutFont[FontPtr++] = (byte)(n);
        }

        protected void WriteFontShort(int n)
        {
            OutFont[FontPtr++] = (byte)(n >> 8);
            OutFont[FontPtr++] = (byte)(n);
        }

        protected void WriteFontString(string s)
        {
            byte[] b = PdfEncodings.ConvertToBytes(s, BaseFont.WINANSI);
            Array.Copy(b, 0, OutFont, FontPtr, b.Length);
            FontPtr += b.Length;
        }
    }
}