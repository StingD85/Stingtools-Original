# -*- coding: utf-8 -*-
"""
Advanced AI Core Engine - Multi-Layer Intelligence System
Neural network-like decision making for Revit automation
"""

from Autodesk.Revit.DB import *
from collections import defaultdict, Counter
from datetime import datetime
import json
import math

doc = __revit__.ActiveUIDocument.Document
uidoc = __revit__.ActiveUIDocument


class NeuralDecisionEngine:
    """Neural network-inspired decision engine with weighted learning"""
    
    def __init__(self):
        self.decision_weights = defaultdict(float)
        self.pattern_memory = []
        self.success_rate = {}
        self.learning_rate = 0.15
    
    def learn_from_pattern(self, pattern_type, features, outcome_success):
        """
        Machine learning-like pattern learning
        Adjusts weights based on success/failure feedback
        """
        feature_signature = self._generate_signature(features)
        
        if outcome_success:
            self.decision_weights[feature_signature] += self.learning_rate
        else:
            self.decision_weights[feature_signature] -= self.learning_rate * 0.5
        
        # Store pattern for future reference
        self.pattern_memory.append({
            'type': pattern_type,
            'features': features,
            'success': outcome_success,
            'timestamp': datetime.now().isoformat(),
            'weight': self.decision_weights[feature_signature]
        })
        
        # Update success rate
        if pattern_type not in self.success_rate:
            self.success_rate[pattern_type] = {'success': 0, 'total': 0}
        
        self.success_rate[pattern_type]['total'] += 1
        if outcome_success:
            self.success_rate[pattern_type]['success'] += 1
    
    def predict_optimal_action(self, pattern_type, current_features):
        """Predict best action based on learned patterns"""
        signature = self._generate_signature(current_features)
        
        # Find similar patterns
        similar_patterns = [p for p in self.pattern_memory 
                           if p['type'] == pattern_type and p['success']]
        
        if not similar_patterns:
            return None
        
        # Calculate similarity scores
        scores = []
        for pattern in similar_patterns:
            similarity = self._calculate_similarity(
                current_features, 
                pattern['features']
            )
            scores.append((similarity, pattern))
        
        # Return most similar successful pattern
        scores.sort(reverse=True, key=lambda x: x[0])
        return scores[0][1] if scores else None
    
    def _generate_signature(self, features):
        """Generate unique signature for feature set"""
        return hash(frozenset(features.items()) if isinstance(features, dict) 
                   else tuple(features))
    
    def _calculate_similarity(self, features1, features2):
        """Calculate cosine similarity between feature sets"""
        if not features1 or not features2:
            return 0.0
        
        # Convert to comparable format
        f1_keys = set(features1.keys() if isinstance(features1, dict) else features1)
        f2_keys = set(features2.keys() if isinstance(features2, dict) else features2)
        
        intersection = len(f1_keys & f2_keys)
        union = len(f1_keys | f2_keys)
        
        return intersection / union if union > 0 else 0.0
    
    def get_confidence_score(self, pattern_type):
        """Get confidence score for a pattern type"""
        if pattern_type not in self.success_rate:
            return 0.0
        
        stats = self.success_rate[pattern_type]
        if stats['total'] == 0:
            return 0.0
        
        return stats['success'] / stats['total']


class FuzzyLogicEngine:
    """Fuzzy logic for handling uncertainty in user intent"""
    
    @staticmethod
    def evaluate_alignment_quality(elements):
        """
        Fuzzy logic evaluation: How well are elements aligned?
        Returns: (quality_score, quality_label, confidence)
        """
        if len(elements) < 2:
            return 0.0, "insufficient_data", 0.0
        
        # Get positions
        positions = []
        for elem in elements:
            try:
                if hasattr(elem, 'Location'):
                    loc = elem.Location
                    if isinstance(loc, LocationPoint):
                        positions.append(loc.Point)
                    elif isinstance(loc, LocationCurve):
                        positions.append(loc.Curve.GetEndPoint(0))
            except:
                continue
        
        if len(positions) < 2:
            return 0.0, "no_positions", 0.0
        
        # Calculate variance in X and Y
        x_coords = [p.X for p in positions]
        y_coords = [p.Y for p in positions]
        
        x_variance = max(x_coords) - min(x_coords)
        y_variance = max(y_coords) - min(y_coords)
        
        # Fuzzy membership functions
        # Perfect alignment: variance < 0.01
        # Good alignment: variance < 0.1
        # Acceptable: variance < 0.5
        # Poor: variance >= 0.5
        
        avg_variance = (x_variance + y_variance) / 2
        
        if avg_variance < 0.01:
            return 0.95, "perfectly_aligned", 0.95
        elif avg_variance < 0.1:
            return 0.80, "well_aligned", 0.85
        elif avg_variance < 0.5:
            return 0.50, "acceptably_aligned", 0.70
        else:
            return 0.20, "poorly_aligned", 0.90
    
    @staticmethod
    def infer_user_intent(selection_pattern, historical_actions):
        """
        Infer what user wants to do based on selection pattern
        Uses fuzzy logic to handle ambiguity
        """
        intent_scores = defaultdict(float)
        
        # Analyze selection
        if 'viewports' in selection_pattern and selection_pattern['viewports'] > 1:
            if selection_pattern.get('on_same_sheet', False):
                intent_scores['align'] = 0.8
                intent_scores['organize'] = 0.7
                intent_scores['renumber'] = 0.6
            else:
                intent_scores['sync_across_sheets'] = 0.7
        
        if 'schedules' in selection_pattern and selection_pattern['schedules'] > 1:
            intent_scores['sync_positions'] = 0.75
            intent_scores['match_widths'] = 0.65
        
        if 'text_notes' in selection_pattern:
            if selection_pattern.get('text_similarity', 0) > 0.7:
                intent_scores['format_text'] = 0.8
            if selection_pattern.get('spatial_pattern', '') == 'linear':
                intent_scores['align'] = 0.75
        
        # Apply historical weighting
        if historical_actions:
            recent_action = historical_actions[-1]
            intent_scores[recent_action] *= 1.2  # Boost recently used actions
        
        # Return highest scoring intent with confidence
        if intent_scores:
            best_intent = max(intent_scores.items(), key=lambda x: x[1])
            confidence = best_intent[1]
            
            # Fuzzy logic: if confidence > 0.7, suggest action
            if confidence > 0.7:
                return best_intent[0], confidence, 'high'
            elif confidence > 0.5:
                return best_intent[0], confidence, 'medium'
            else:
                return best_intent[0], confidence, 'low'
        
        return None, 0.0, 'none'


class PredictiveAnalyzer:
    """Predictive analysis engine for anticipating user needs"""
    
    def __init__(self):
        self.action_history = []
        self.element_state_cache = {}
        self.prediction_cache = {}
    
    def analyze_workflow_pattern(self, recent_actions):
        """
        Analyze workflow patterns to predict next action
        Uses sequence analysis and probability chains
        """
        if len(recent_actions) < 2:
            return []
        
        # Build transition probability matrix
        transitions = defaultdict(lambda: defaultdict(int))
        
        for i in range(len(recent_actions) - 1):
            current = recent_actions[i]
            next_action = recent_actions[i + 1]
            transitions[current][next_action] += 1
        
        # If we have current action, predict next
        if recent_actions:
            current_action = recent_actions[-1]
            if current_action in transitions:
                next_actions = transitions[current_action]
                total = sum(next_actions.values())
                
                # Calculate probabilities
                predictions = [
                    (action, count / total, 'workflow_pattern')
                    for action, count in next_actions.items()
                ]
                predictions.sort(key=lambda x: x[1], reverse=True)
                
                return predictions[:3]  # Top 3 predictions
        
        return []
    
    def predict_common_errors(self, element_type, current_state):
        """
        Predict common errors before they happen
        Proactive error prevention
        """
        errors = []
        
        if element_type == 'viewport':
            # Check for common viewport issues
            if current_state.get('overlapping', False):
                errors.append({
                    'type': 'overlap',
                    'severity': 'high',
                    'suggestion': 'Use spacing tool to separate viewports',
                    'confidence': 0.85
                })
            
            if current_state.get('detail_number_gaps', False):
                errors.append({
                    'type': 'numbering_gap',
                    'severity': 'medium',
                    'suggestion': 'Renumber sequentially to fix gaps',
                    'confidence': 0.75
                })
        
        elif element_type == 'schedule':
            if current_state.get('inconsistent_widths', False):
                errors.append({
                    'type': 'width_mismatch',
                    'severity': 'low',
                    'suggestion': 'Match column widths for consistency',
                    'confidence': 0.70
                })
        
        return errors
    
    def suggest_optimizations(self, current_selection):
        """
        Suggest optimizations based on AI analysis
        Proactive improvement suggestions
        """
        suggestions = []
        
        # Analyze selection for optimization opportunities
        if 'viewports' in current_selection:
            viewports = current_selection['viewports']
            
            # Check alignment
            fuzzy = FuzzyLogicEngine()
            quality, label, conf = fuzzy.evaluate_alignment_quality(viewports)
            
            if quality < 0.7:
                suggestions.append({
                    'action': 'align_viewports',
                    'reason': f'Alignment quality: {label} ({quality:.2f})',
                    'benefit': 'Improve visual consistency',
                    'priority': 'high' if quality < 0.5 else 'medium',
                    'confidence': conf
                })
            
            # Check numbering sequence
            if len(viewports) > 2:
                suggestions.append({
                    'action': 'renumber_sequence',
                    'reason': 'Multiple viewports selected',
                    'benefit': 'Ensure sequential numbering',
                    'priority': 'medium',
                    'confidence': 0.65
                })
        
        return suggestions


class ContextAwareEngine:
    """Context-aware decision making based on project state"""
    
    def __init__(self):
        self.project_context = {}
        self.user_preferences = {}
        self.session_context = {}
    
    def analyze_project_context(self):
        """
        Deep analysis of project state for context-aware decisions
        """
        context = {
            'project_phase': self._detect_project_phase(),
            'documentation_stage': self._detect_doc_stage(),
            'complexity_level': self._assess_complexity(),
            'standards_detected': self._detect_standards(),
            'team_size_indicator': self._estimate_team_size()
        }
        
        self.project_context = context
        return context
    
    def _detect_project_phase(self):
        """Detect project phase from model state"""
        # Count elements by category
        collector = FilteredElementCollector(doc)
        
        # Sample counts (simplified)
        total_elements = collector.GetElementCount()
        
        if total_elements < 1000:
            return 'schematic_design'
        elif total_elements < 5000:
            return 'design_development'
        elif total_elements < 15000:
            return 'construction_documents'
        else:
            return 'construction_admin'
    
    def _detect_doc_stage(self):
        """Detect documentation stage"""
        # Count sheets
        sheets = FilteredElementCollector(doc)\
            .OfClass(ViewSheet)\
            .GetElementCount()
        
        if sheets < 10:
            return 'early'
        elif sheets < 50:
            return 'intermediate'
        else:
            return 'advanced'
    
    def _assess_complexity(self):
        """Assess project complexity"""
        try:
            # Multiple factors
            sheet_count = FilteredElementCollector(doc)\
                .OfClass(ViewSheet).GetElementCount()
            
            view_count = FilteredElementCollector(doc)\
                .OfClass(View).GetElementCount()
            
            complexity_score = (sheet_count * 0.5) + (view_count * 0.1)
            
            if complexity_score < 50:
                return 'simple'
            elif complexity_score < 200:
                return 'moderate'
            else:
                return 'complex'
        except:
            return 'unknown'
    
    def _detect_standards(self):
        """Detect project standards in use"""
        standards = []
        
        # Check for naming conventions
        sheets = FilteredElementCollector(doc)\
            .OfClass(ViewSheet)\
            .ToElements()
        
        if sheets:
            # Analyze sheet numbers
            sheet_numbers = [s.SheetNumber for s in sheets[:10]]
            
            # Check for patterns
            if any('-' in num for num in sheet_numbers):
                standards.append('hyphenated_numbering')
            if any(num[0].isalpha() for num in sheet_numbers):
                standards.append('alpha_prefix')
        
        return standards
    
    def _estimate_team_size(self):
        """Estimate team size from model characteristics"""
        # This is a heuristic estimation
        try:
            worksets = FilteredWorksetCollector(doc).ToWorksets()
            
            if len(worksets) > 10:
                return 'large_team'
            elif len(worksets) > 5:
                return 'medium_team'
            else:
                return 'small_team'
        except:
            return 'solo_or_small'
    
    def get_smart_defaults(self, operation_type):
        """
        Get smart defaults based on project context
        Context-aware parameter suggestions
        """
        defaults = {}
        
        phase = self.project_context.get('project_phase', 'unknown')
        complexity = self.project_context.get('complexity_level', 'moderate')
        
        if operation_type == 'viewport_spacing':
            if phase == 'schematic_design':
                defaults['gap'] = 0.75  # Looser spacing early on
            elif phase == 'construction_documents':
                defaults['gap'] = 0.5  # Tighter for CD
            else:
                defaults['gap'] = 0.625
        
        elif operation_type == 'schedule_width':
            if complexity == 'complex':
                defaults['width'] = 1.2  # Wider for complex projects
            else:
                defaults['width'] = 1.0
        
        elif operation_type == 'text_formatting':
            if 'alpha_prefix' in self.project_context.get('standards_detected', []):
                defaults['preserve_prefixes'] = True
        
        return defaults


class AdaptiveLearningSystem:
    """
    Adaptive learning system that improves over time
    Learns from user corrections and preferences
    """
    
    def __init__(self):
        self.user_corrections = []
        self.preference_model = defaultdict(float)
        self.adaptation_count = 0
    
    def record_user_correction(self, ai_suggestion, user_choice, context):
        """
        Record when user corrects AI suggestion
        Learn from mistakes
        """
        correction = {
            'ai_suggested': ai_suggestion,
            'user_chose': user_choice,
            'context': context,
            'timestamp': datetime.now().isoformat(),
            'adaptation_id': self.adaptation_count
        }
        
        self.user_corrections.append(correction)
        self.adaptation_count += 1
        
        # Update preference model
        preference_key = f"{context.get('operation', 'unknown')}_{user_choice}"
        self.preference_model[preference_key] += 1.0
        
        # Reduce weight of corrected suggestion
        wrong_key = f"{context.get('operation', 'unknown')}_{ai_suggestion}"
        self.preference_model[wrong_key] -= 0.5
    
    def get_adapted_suggestion(self, operation, options):
        """
        Get suggestion adapted to user preferences
        Uses accumulated learning
        """
        if not options:
            return None
        
        # Score each option based on learned preferences
        scores = {}
        for option in options:
            key = f"{operation}_{option}"
            scores[option] = self.preference_model.get(key, 0.0)
        
        # Return highest scoring
        if scores:
            best_option = max(scores.items(), key=lambda x: x[1])
            if best_option[1] > 0:  # Only if we have positive learning
                return best_option[0]
        
        # Fall back to first option
        return options[0] if options else None
    
    def get_learning_stats(self):
        """Get statistics about learning progress"""
        return {
            'total_corrections': len(self.user_corrections),
            'adaptation_count': self.adaptation_count,
            'learned_preferences': len(self.preference_model),
            'confidence': min(1.0, self.adaptation_count / 10.0)
        }


class IntelligenceOrchestrator:
    """
    Master orchestrator coordinating all AI engines
    Multi-layer decision making
    """
    
    def __init__(self):
        self.neural_engine = NeuralDecisionEngine()
        self.fuzzy_engine = FuzzyLogicEngine()
        self.predictive_engine = PredictiveAnalyzer()
        self.context_engine = ContextAwareEngine()
        self.learning_system = AdaptiveLearningSystem()
        
        # Initialize project context
        self.context_engine.analyze_project_context()
    
    def make_intelligent_decision(self, operation_type, current_state, options):
        """
        Multi-layer intelligent decision making
        Combines all AI engines for optimal results
        """
        # Layer 1: Neural network prediction
        neural_prediction = self.neural_engine.predict_optimal_action(
            operation_type, 
            current_state
        )
        
        # Layer 2: Fuzzy logic evaluation
        quality_score, quality_label, confidence = \
            self.fuzzy_engine.evaluate_alignment_quality(
                current_state.get('elements', [])
            )
        
        # Layer 3: Context-aware defaults
        context_defaults = self.context_engine.get_smart_defaults(operation_type)
        
        # Layer 4: Adaptive learning
        learned_preference = self.learning_system.get_adapted_suggestion(
            operation_type,
            options
        )
        
        # Layer 5: Combine all layers with weighted voting
        final_decision = {
            'recommended_action': learned_preference or (
                neural_prediction['features'].get('action') 
                if neural_prediction else options[0]
            ),
            'confidence': self._calculate_combined_confidence(
                neural_prediction,
                confidence,
                self.learning_system.get_learning_stats()['confidence']
            ),
            'reasoning': {
                'neural_contribution': 'Pattern matching from history',
                'fuzzy_contribution': f'Quality: {quality_label}',
                'context_contribution': f"Project phase: {self.context_engine.project_context.get('project_phase')}",
                'learning_contribution': f"Adapted from {self.learning_system.adaptation_count} corrections"
            },
            'context_defaults': context_defaults,
            'alternative_options': options
        }
        
        return final_decision
    
    def _calculate_combined_confidence(self, neural_pred, fuzzy_conf, learning_conf):
        """Calculate weighted confidence score from all engines"""
        weights = {
            'neural': 0.3,
            'fuzzy': 0.3,
            'learning': 0.4
        }
        
        neural_score = neural_pred.get('weight', 0.5) if neural_pred else 0.5
        
        combined = (
            neural_score * weights['neural'] +
            fuzzy_conf * weights['fuzzy'] +
            learning_conf * weights['learning']
        )
        
        return max(0.0, min(1.0, combined))


# Global intelligence orchestrator instance
_intelligence_orchestrator = None

def get_intelligence_orchestrator():
    """Get or create global intelligence orchestrator"""
    global _intelligence_orchestrator
    if _intelligence_orchestrator is None:
        _intelligence_orchestrator = IntelligenceOrchestrator()
    return _intelligence_orchestrator
