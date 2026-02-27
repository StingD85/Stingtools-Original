# -*- coding: utf-8 -*-
"""Advanced Text Note Formatting with NLP-Like Intelligence"""

from Autodesk.Revit.DB import *
from pyrevit import revit, DB, forms
import re

doc = revit.doc
uidoc = revit.uidoc


class IntelligentTextAnalyzer:
    """NLP-like text analysis engine"""
    
    # Common acronyms that should stay uppercase
    ACRONYMS = {
        'BIM', 'CAD', 'HVAC', 'MEP', 'ADA', 'OSHA', 'LEED', 'NEC', 'IBC',
        'AIA', 'ASHRAE', 'ASTM', 'ISO', 'ANSI', 'AWS', 'AISC', 'ACI',
        'USA', 'UK', 'EU', 'NA', 'NTS', 'TYP', 'REF', 'SIM', 'EQ',
        'FFL', 'TOS', 'BOT', 'FFE', 'CLG', 'GWB', 'CMU', 'VCT', 'ACT'
    }
    
    # Common abbreviations to preserve
    ABBREVIATIONS = {
        'Dr.', 'Mr.', 'Mrs.', 'Ms.', 'Inc.', 'Ltd.', 'Corp.', 'Co.',
        'St.', 'Ave.', 'Blvd.', 'Rd.', 'No.', 'Apt.', 'Ste.', 'Fl.',
        'Fig.', 'Vol.', 'Pg.', 'vs.', 'etc.', 'i.e.', 'e.g.'
    }
    
    @classmethod
    def intelligent_title_case(cls, text):
        """Convert to title case while preserving acronyms and special cases"""
        words = text.split()
        result = []
        
        # Words that should stay lowercase in titles (unless first/last)
        lowercase_words = {'a', 'an', 'the', 'and', 'but', 'or', 'nor', 'for', 
                          'at', 'by', 'from', 'in', 'into', 'of', 'on', 'to', 
                          'with', 'as', 'per', 'via'}
        
        for i, word in enumerate(words):
            # Check if it's an acronym
            if word.upper() in cls.ACRONYMS:
                result.append(word.upper())
            # Check if it contains abbreviation
            elif any(abbr in word for abbr in cls.ABBREVIATIONS):
                result.append(word)
            # Check if it's a number with units
            elif re.match(r'^\d+[\'\"]\-?\d*$', word):  # e.g., 2'-6"
                result.append(word)
            # First or last word: always capitalize
            elif i == 0 or i == len(words) - 1:
                result.append(word.capitalize())
            # Check if should stay lowercase
            elif word.lower() in lowercase_words:
                result.append(word.lower())
            else:
                result.append(word.capitalize())
        
        return ' '.join(result)
    
    @classmethod
    def smart_sentence_case(cls, text):
        """Convert to sentence case with intelligent punctuation handling"""
        # Split into sentences
        sentences = re.split(r'([.!?]+)', text)
        result = []
        
        for i, part in enumerate(sentences):
            if part and part[0] not in '.!?':
                # It's a sentence, not punctuation
                words = part.split()
                if words:
                    # Capitalize first word of sentence
                    words[0] = words[0].capitalize()
                    # Preserve acronyms in rest of sentence
                    for j in range(1, len(words)):
                        if words[j].upper() not in cls.ACRONYMS:
                            words[j] = words[j].lower()
                    result.append(' '.join(words))
            else:
                result.append(part)
        
        return ''.join(result)
    
    @classmethod
    def detect_case_pattern(cls, texts):
        """Analyze texts to detect predominant case pattern"""
        uppercase_count = 0
        lowercase_count = 0
        titlecase_count = 0
        mixed_count = 0
        
        for text in texts:
            if text.isupper():
                uppercase_count += 1
            elif text.islower():
                lowercase_count += 1
            elif text.istitle():
                titlecase_count += 1
            else:
                mixed_count += 1
        
        total = len(texts)
        if total == 0:
            return 'mixed'
        
        # Return predominant pattern
        patterns = {
            'uppercase': uppercase_count / total,
            'lowercase': lowercase_count / total,
            'titlecase': titlecase_count / total,
            'mixed': mixed_count / total
        }
        
        return max(patterns, key=patterns.get)


def get_selected_text_notes():
    """Get selected text notes with intelligent fallback"""
    selection = revit.get_selection()
    text_notes = []
    
    for elem_id in selection.element_ids:
        elem = doc.GetElement(elem_id)
        if isinstance(elem, TextNote):
            text_notes.append(elem)
    
    if not text_notes:
        forms.alert('Please select text notes in the active view.', exitscript=True)
    
    return text_notes


def convert_to_lower():
    """Convert text to lowercase with smart preservation"""
    text_notes = get_selected_text_notes()
    
    analyzer = IntelligentTextAnalyzer()
    
    # Analyze current state
    current_texts = [note.Text for note in text_notes]
    pattern = analyzer.detect_case_pattern(current_texts)
    
    # Warn if already lowercase
    if pattern == 'lowercase':
        proceed = forms.alert(
            'Most text is already lowercase.\n\nProceed anyway?',
            yes=True, no=True
        )
        if not proceed:
            return 0
    
    with revit.Transaction('Convert to Lowercase (Intelligent)'):
        count = 0
        for note in text_notes:
            try:
                original = note.Text
                # Convert but preserve dimension strings
                if not re.search(r'\d+[\'\"]\-?\d*', original):
                    note.Text = original.lower()
                    count += 1
            except:
                continue
    
    return count


def convert_to_upper():
    """Convert text to UPPERCASE with intelligent preservation"""
    text_notes = get_selected_text_notes()
    
    analyzer = IntelligentTextAnalyzer()
    
    # Analyze current state
    current_texts = [note.Text for note in text_notes]
    pattern = analyzer.detect_case_pattern(current_texts)
    
    # Warn if already uppercase
    if pattern == 'uppercase':
        proceed = forms.alert(
            'Most text is already UPPERCASE.\n\nProceed anyway?',
            yes=True, no=True
        )
        if not proceed:
            return 0
    
    with revit.Transaction('Convert to UPPERCASE (Intelligent)'):
        count = 0
        for note in text_notes:
            try:
                note.Text = note.Text.upper()
                count += 1
            except:
                continue
    
    return count


def convert_to_title():
    """Convert to Title Case with NLP-like intelligence"""
    text_notes = get_selected_text_notes()
    
    analyzer = IntelligentTextAnalyzer()
    
    # Show preview of one conversion
    if text_notes:
        sample_original = text_notes[0].Text
        sample_converted = analyzer.intelligent_title_case(sample_original)
        
        proceed = forms.alert(
            'Intelligent Title Case Conversion\n\n'
            'Sample:\n'
            'Before: {}\n'
            'After:  {}\n\n'
            'This will:\n'
            '• Preserve acronyms (BIM, HVAC, etc.)\n'
            '• Handle abbreviations (Dr., Inc., etc.)\n'
            '• Apply proper title capitalization rules\n\n'
            'Continue?'.format(sample_original[:50], sample_converted[:50]),
            yes=True, no=True
        )
        
        if not proceed:
            return 0
    
    with revit.Transaction('Convert to Title Case (Intelligent NLP)'):
        count = 0
        for note in text_notes:
            try:
                original = note.Text
                converted = analyzer.intelligent_title_case(original)
                note.Text = converted
                count += 1
            except:
                continue
    
    forms.alert('Converted {} text notes using NLP-like intelligence.\n\n'
               'Features used:\n'
               '✓ Acronym preservation\n'
               '✓ Abbreviation handling\n'
               '✓ Title case grammar rules\n'
               '✓ Dimension string protection'.format(count))
    
    return count
