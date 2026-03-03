// ============================================================================
// StingBIM AI - Acoustic Model
// Provides acoustic analysis for room design and sound transmission
// Based on ISO 3382, BS EN 12354, and CIBSE Guide A
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.AI.Intelligence.Physics
{
    /// <summary>
    /// Acoustic analysis model for building design
    /// Calculates reverberation time, speech intelligibility, and sound transmission
    /// </summary>
    public class AcousticModel
    {
        private readonly Dictionary<string, MaterialAcousticData> _materialDatabase;
        private readonly Dictionary<string, RoomAcousticTarget> _roomTargets;

        public AcousticModel()
        {
            _materialDatabase = LoadMaterialDatabase();
            _roomTargets = LoadRoomTargets();
        }

        #region Room Acoustics Analysis

        /// <summary>
        /// Analyze room acoustics and provide recommendations
        /// </summary>
        public RoomAcousticAnalysis AnalyzeRoom(RoomGeometry room, List<SurfaceMaterial> surfaces)
        {
            var analysis = new RoomAcousticAnalysis
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                RoomType = room.RoomType,
                Volume = room.Volume,
                FloorArea = room.FloorArea
            };

            // Calculate total absorption at each frequency
            analysis.AbsorptionByFrequency = CalculateTotalAbsorption(surfaces, room);

            // Calculate reverberation time (Sabine formula)
            analysis.RT60ByFrequency = CalculateRT60(room.Volume, analysis.AbsorptionByFrequency);

            // Calculate mid-frequency RT60 (average of 500Hz and 1000Hz)
            analysis.RT60Mid = (analysis.RT60ByFrequency[500] + analysis.RT60ByFrequency[1000]) / 2.0;

            // Get target for room type
            if (_roomTargets.TryGetValue(room.RoomType, out var target))
            {
                analysis.TargetRT60 = target;
                analysis.MeetsTarget = analysis.RT60Mid >= target.MinRT60 && analysis.RT60Mid <= target.MaxRT60;
            }

            // Calculate speech intelligibility metrics
            analysis.SpeechTransmissionIndex = CalculateSTI(analysis.RT60Mid, room.Volume);
            analysis.SpeechIntelligibility = ClassifySpeechIntelligibility(analysis.SpeechTransmissionIndex);

            // Calculate clarity (C50 for speech, C80 for music)
            analysis.C50 = CalculateClarity(analysis.RT60Mid, 0.05);
            analysis.C80 = CalculateClarity(analysis.RT60Mid, 0.08);

            // Generate recommendations
            analysis.Recommendations = GenerateAcousticRecommendations(analysis, surfaces);

            return analysis;
        }

        /// <summary>
        /// Calculate Sabine reverberation time
        /// </summary>
        public Dictionary<int, double> CalculateRT60(double volume, Dictionary<int, double> totalAbsorption)
        {
            var rt60 = new Dictionary<int, double>();

            foreach (var kvp in totalAbsorption)
            {
                // Sabine formula: RT60 = 0.161 * V / A
                if (kvp.Value > 0)
                {
                    rt60[kvp.Key] = 0.161 * volume / kvp.Value;
                }
                else
                {
                    rt60[kvp.Key] = double.PositiveInfinity;
                }
            }

            return rt60;
        }

        /// <summary>
        /// Calculate Eyring reverberation time (for high absorption rooms)
        /// </summary>
        public double CalculateRT60Eyring(double volume, double surfaceArea, double averageAlpha)
        {
            if (averageAlpha >= 1.0) return 0;

            // Eyring formula: RT60 = 0.161 * V / (-S * ln(1 - α))
            return 0.161 * volume / (-surfaceArea * Math.Log(1 - averageAlpha));
        }

        private Dictionary<int, double> CalculateTotalAbsorption(List<SurfaceMaterial> surfaces, RoomGeometry room)
        {
            var frequencies = new[] { 125, 250, 500, 1000, 2000, 4000 };
            var absorption = frequencies.ToDictionary(f => f, f => 0.0);

            foreach (var surface in surfaces)
            {
                if (_materialDatabase.TryGetValue(surface.MaterialName, out var materialData))
                {
                    foreach (var freq in frequencies)
                    {
                        var alpha = materialData.AbsorptionCoefficients.GetValueOrDefault(freq, 0.1);
                        absorption[freq] += surface.Area * alpha;
                    }
                }
                else
                {
                    // Default absorption for unknown materials
                    foreach (var freq in frequencies)
                    {
                        absorption[freq] += surface.Area * 0.05;
                    }
                }
            }

            // Add air absorption for high frequencies (significant for large rooms)
            var airAbsorption = CalculateAirAbsorption(room.Volume, room.Temperature, room.RelativeHumidity);
            foreach (var freq in frequencies.Where(f => f >= 2000))
            {
                absorption[freq] += airAbsorption[freq];
            }

            return absorption;
        }

        private Dictionary<int, double> CalculateAirAbsorption(double volume, double temperature, double humidity)
        {
            // Air absorption calculation based on ISO 9613-1
            // Air absorption coefficient varies with frequency, temperature, and humidity

            var airAbsorption = new Dictionary<int, double>();

            // Reference coefficients at 20°C, 50% RH (Nepers/m)
            var refCoefficients = new Dictionary<int, double>
            {
                { 125, 0.0001 },
                { 250, 0.0003 },
                { 500, 0.0006 },
                { 1000, 0.001 },
                { 2000, 0.003 },
                { 4000, 0.01 }
            };

            // Temperature correction: absorption increases with temperature
            // ISO 9613-1 simplified relationship
            double tempKelvin = temperature + 273.15;
            double refTempKelvin = 293.15; // 20°C
            double tempRatio = tempKelvin / refTempKelvin;

            // Humidity correction: absorption decreases with higher humidity at high frequencies
            // Reference humidity 50%, correction factor based on molar concentration of water vapor
            double refHumidity = 50.0;
            double humidityRatio = humidity > 0 ? refHumidity / humidity : 2.0;

            // Apply corrections per frequency band
            // Low frequencies (<500Hz): minimal temp/humidity sensitivity
            // Mid frequencies (500-2000Hz): moderate sensitivity
            // High frequencies (>2000Hz): strong sensitivity (proportional to humidity)
            foreach (var kvp in refCoefficients)
            {
                double correctedCoeff = kvp.Value;
                int freq = kvp.Key;

                if (freq <= 500)
                {
                    // Low frequencies: weak temp dependence, negligible humidity dependence
                    correctedCoeff *= Math.Pow(tempRatio, 0.5);
                }
                else if (freq <= 2000)
                {
                    // Mid frequencies: moderate dependence on both
                    correctedCoeff *= Math.Pow(tempRatio, 1.0) * Math.Pow(humidityRatio, 0.3);
                }
                else
                {
                    // High frequencies: strong dependence, especially on humidity
                    // In hot dry climates (Africa), high-freq absorption is significantly higher
                    correctedCoeff *= Math.Pow(tempRatio, 1.5) * Math.Pow(humidityRatio, 0.6);
                }

                // Clamp to physical limits
                correctedCoeff = Math.Max(0.00001, Math.Min(correctedCoeff, 0.1));

                // 4mV term in Sabine equation
                airAbsorption[kvp.Key] = 4 * correctedCoeff * volume;
            }

            return airAbsorption;
        }

        #endregion

        #region Speech Intelligibility

        /// <summary>
        /// Calculate Speech Transmission Index (simplified)
        /// </summary>
        public double CalculateSTI(double rt60, double volume)
        {
            // Simplified STI calculation based on RT60
            // Full STI requires modulation transfer function measurement

            // Approximate relationship: STI ≈ 0.9 - 0.2 * log10(RT60)
            // Valid for RT60 between 0.3 and 3 seconds

            if (rt60 < 0.3) rt60 = 0.3;
            if (rt60 > 3.0) rt60 = 3.0;

            double sti = 0.9 - 0.2 * Math.Log10(rt60);

            // Volume correction for very large rooms
            if (volume > 1000)
            {
                sti -= 0.05 * Math.Log10(volume / 1000);
            }

            return Math.Max(0, Math.Min(1, sti));
        }

        private string ClassifySpeechIntelligibility(double sti)
        {
            if (sti >= 0.75) return "Excellent";
            if (sti >= 0.60) return "Good";
            if (sti >= 0.45) return "Fair";
            if (sti >= 0.30) return "Poor";
            return "Bad";
        }

        /// <summary>
        /// Calculate clarity index
        /// </summary>
        private double CalculateClarity(double rt60, double earlyTime)
        {
            // C = 10 * log10(early energy / late energy)
            // Approximation based on exponential decay

            double decayConstant = rt60 / 6.9; // Time constant
            double earlyRatio = 1 - Math.Exp(-earlyTime / decayConstant);
            double lateRatio = Math.Exp(-earlyTime / decayConstant);

            if (lateRatio <= 0) return 20; // Maximum practical value

            return 10 * Math.Log10(earlyRatio / lateRatio);
        }

        #endregion

        #region Sound Transmission

        /// <summary>
        /// Calculate apparent sound reduction index between spaces
        /// </summary>
        public SoundTransmissionAnalysis AnalyzeSoundTransmission(
            PartitionAssembly partition,
            RoomGeometry sourceRoom,
            RoomGeometry receiverRoom)
        {
            var analysis = new SoundTransmissionAnalysis
            {
                PartitionId = partition.PartitionId,
                SourceRoomId = sourceRoom.RoomId,
                ReceiverRoomId = receiverRoom.RoomId
            };

            // Calculate weighted sound reduction index (Rw)
            analysis.Rw = CalculateWeightedSoundReduction(partition);

            // Calculate apparent sound reduction (R'w) considering flanking
            analysis.RwApparent = CalculateApparentSoundReduction(
                analysis.Rw,
                partition.Area,
                receiverRoom.Volume);

            // Calculate DnT,w (standardized level difference)
            analysis.DnTw = analysis.RwApparent + 10 * Math.Log10(receiverRoom.Volume / (0.5 * partition.Area));

            // Check compliance with standards
            analysis.ComplianceResults = CheckAcousticCompliance(analysis, sourceRoom.RoomType, receiverRoom.RoomType);

            // Identify weak points
            analysis.WeakPoints = IdentifyWeakPoints(partition);

            return analysis;
        }

        /// <summary>
        /// Calculate weighted sound reduction index
        /// </summary>
        public double CalculateWeightedSoundReduction(PartitionAssembly partition)
        {
            // Mass law approximation for single leaf: Rw ≈ 20 * log10(m * f) - 47
            // For double leaf with cavity: Rw = Rw1 + Rw2 + improvement factor

            if (partition.IsDoubleLeaf)
            {
                double rw1 = CalculateSingleLeafRw(partition.Mass1, partition.Thickness1);
                double rw2 = CalculateSingleLeafRw(partition.Mass2, partition.Thickness2);

                // Cavity improvement (depends on cavity depth and insulation)
                double cavityImprovement = CalculateCavityImprovement(
                    partition.CavityDepth,
                    partition.HasCavityInsulation);

                return rw1 + rw2 + cavityImprovement - 3; // -3 dB for coincidence
            }
            else
            {
                return CalculateSingleLeafRw(partition.Mass1, partition.Thickness1);
            }
        }

        private double CalculateSingleLeafRw(double massPerM2, double thickness)
        {
            // Simplified mass law
            double fc = 12000 / thickness; // Critical frequency approximation
            double rw = 20 * Math.Log10(massPerM2) + 12;

            // Coincidence dip correction
            if (fc < 3150)
            {
                rw -= 3;
            }

            return Math.Min(rw, 60); // Practical maximum
        }

        private double CalculateCavityImprovement(double cavityDepth, bool hasInsulation)
        {
            double improvement = 0;

            // Depth contribution
            if (cavityDepth >= 100) improvement += 6;
            else if (cavityDepth >= 50) improvement += 3;

            // Insulation contribution
            if (hasInsulation) improvement += 5;

            return improvement;
        }

        private double CalculateApparentSoundReduction(double rw, double partitionArea, double receiverVolume)
        {
            // Account for flanking transmission (simplified)
            // R'w ≈ Rw - 2 for typical construction
            // More flanking in lightweight construction

            double flankingReduction = 2.0;

            // Additional reduction for small partitions (more flanking relative to direct)
            if (partitionArea < 10) flankingReduction += 1;

            return rw - flankingReduction;
        }

        private List<ComplianceResult> CheckAcousticCompliance(
            SoundTransmissionAnalysis analysis,
            string sourceRoomType,
            string receiverRoomType)
        {
            var results = new List<ComplianceResult>();

            // Building Regulations requirements (Approved Document E / BS 8233)
            var requirements = GetAcousticRequirements(sourceRoomType, receiverRoomType);

            foreach (var req in requirements)
            {
                results.Add(new ComplianceResult
                {
                    Standard = req.Standard,
                    Requirement = req.Description,
                    RequiredValue = req.RequiredDnTw,
                    ActualValue = analysis.DnTw,
                    Compliant = analysis.DnTw >= req.RequiredDnTw,
                    Margin = analysis.DnTw - req.RequiredDnTw
                });
            }

            return results;
        }

        private List<AcousticRequirement> GetAcousticRequirements(string sourceType, string receiverType)
        {
            var requirements = new List<AcousticRequirement>();

            // Residential separating walls/floors
            if (IsResidential(sourceType) && IsResidential(receiverType))
            {
                requirements.Add(new AcousticRequirement
                {
                    Standard = "Building Regs Part E",
                    Description = "Airborne sound insulation - separating walls",
                    RequiredDnTw = 45
                });
            }

            // Office to office
            if (sourceType == "Office" && receiverType == "Office")
            {
                requirements.Add(new AcousticRequirement
                {
                    Standard = "BS 8233",
                    Description = "Open plan to cellular office",
                    RequiredDnTw = 35
                });
            }

            // Meeting room to office
            if (sourceType == "Meeting Room" || receiverType == "Meeting Room")
            {
                requirements.Add(new AcousticRequirement
                {
                    Standard = "BCO Guide",
                    Description = "Confidential meeting room",
                    RequiredDnTw = 45
                });
            }

            return requirements;
        }

        private bool IsResidential(string roomType)
        {
            var residentialTypes = new HashSet<string>
            {
                "Bedroom", "Living Room", "Kitchen", "Bathroom", "Apartment", "Flat", "House"
            };
            return residentialTypes.Contains(roomType);
        }

        private List<AcousticWeakPoint> IdentifyWeakPoints(PartitionAssembly partition)
        {
            var weakPoints = new List<AcousticWeakPoint>();

            // Check doors
            if (partition.HasDoor && partition.DoorRw < partition.OverallRw - 10)
            {
                weakPoints.Add(new AcousticWeakPoint
                {
                    Element = "Door",
                    Issue = "Door acoustic rating significantly below wall",
                    Severity = "High",
                    Recommendation = "Upgrade to acoustic door (Rw 35+) or add acoustic seals"
                });
            }

            // Check gaps and seals
            if (!partition.HasPerimeterSeals)
            {
                weakPoints.Add(new AcousticWeakPoint
                {
                    Element = "Perimeter",
                    Issue = "Missing perimeter seals",
                    Severity = "High",
                    Recommendation = "Install acoustic sealant at all junctions"
                });
            }

            // Check service penetrations
            if (partition.HasUnseakedPenetrations)
            {
                weakPoints.Add(new AcousticWeakPoint
                {
                    Element = "Penetrations",
                    Issue = "Unsealed service penetrations",
                    Severity = "Medium",
                    Recommendation = "Seal all penetrations with intumescent acoustic sealant"
                });
            }

            // Check back-to-back sockets
            if (partition.HasBackToBackSockets)
            {
                weakPoints.Add(new AcousticWeakPoint
                {
                    Element = "Electrical",
                    Issue = "Back-to-back socket outlets",
                    Severity = "Medium",
                    Recommendation = "Offset outlets by 300mm minimum and add acoustic putty pads"
                });
            }

            return weakPoints;
        }

        #endregion

        #region Recommendations

        private List<AcousticRecommendation> GenerateAcousticRecommendations(
            RoomAcousticAnalysis analysis,
            List<SurfaceMaterial> currentSurfaces)
        {
            var recommendations = new List<AcousticRecommendation>();

            if (!analysis.MeetsTarget && analysis.TargetRT60 != null)
            {
                if (analysis.RT60Mid > analysis.TargetRT60.MaxRT60)
                {
                    // Room is too reverberant - need more absorption
                    recommendations.AddRange(GenerateAbsorptionRecommendations(
                        analysis, currentSurfaces, analysis.TargetRT60.MaxRT60));
                }
                else if (analysis.RT60Mid < analysis.TargetRT60.MinRT60)
                {
                    // Room is too dead - reduce absorption
                    recommendations.Add(new AcousticRecommendation
                    {
                        Category = "Room Acoustics",
                        Priority = "Medium",
                        Issue = $"RT60 ({analysis.RT60Mid:F2}s) below target ({analysis.TargetRT60.MinRT60}s)",
                        Recommendation = "Reduce soft furnishings or replace acoustic ceiling with standard tiles",
                        ExpectedImprovement = "Increase RT60 by 0.1-0.3s"
                    });
                }
            }

            // Speech intelligibility recommendations
            if (analysis.SpeechTransmissionIndex < 0.6 &&
                (analysis.RoomType == "Classroom" || analysis.RoomType == "Meeting Room" ||
                 analysis.RoomType == "Lecture Hall"))
            {
                recommendations.Add(new AcousticRecommendation
                {
                    Category = "Speech Intelligibility",
                    Priority = "High",
                    Issue = $"STI of {analysis.SpeechTransmissionIndex:F2} indicates {analysis.SpeechIntelligibility} speech intelligibility",
                    Recommendation = "Add absorptive panels to rear wall and install speech reinforcement system",
                    ExpectedImprovement = "STI improvement of 0.1-0.2"
                });
            }

            return recommendations;
        }

        private List<AcousticRecommendation> GenerateAbsorptionRecommendations(
            RoomAcousticAnalysis analysis,
            List<SurfaceMaterial> currentSurfaces,
            double targetRT60)
        {
            var recommendations = new List<AcousticRecommendation>();

            // Calculate required additional absorption
            double currentAbsorption = analysis.AbsorptionByFrequency[1000];
            double requiredAbsorption = 0.161 * analysis.Volume / targetRT60;
            double additionalAbsorption = requiredAbsorption - currentAbsorption;

            if (additionalAbsorption > 0)
            {
                // Ceiling treatment
                var ceiling = currentSurfaces.FirstOrDefault(s => s.SurfaceType == "Ceiling");
                if (ceiling != null && !ceiling.MaterialName.Contains("Acoustic"))
                {
                    double potentialAbsorption = ceiling.Area * 0.7; // Acoustic tile absorption
                    recommendations.Add(new AcousticRecommendation
                    {
                        Category = "Ceiling Treatment",
                        Priority = "High",
                        Issue = "Standard ceiling provides minimal absorption",
                        Recommendation = "Install acoustic ceiling tiles (NRC 0.70+)",
                        ExpectedImprovement = $"Additional {potentialAbsorption:F0} m² Sabins absorption"
                    });
                }

                // Wall panels
                if (additionalAbsorption > 20)
                {
                    double wallPanelArea = additionalAbsorption / 0.8; // NRC 0.80 panels
                    recommendations.Add(new AcousticRecommendation
                    {
                        Category = "Wall Treatment",
                        Priority = "Medium",
                        Issue = "Insufficient absorption for target RT60",
                        Recommendation = $"Install {wallPanelArea:F0} m² of acoustic wall panels",
                        ExpectedImprovement = $"RT60 reduction to approximately {targetRT60:F2}s"
                    });
                }
            }

            return recommendations;
        }

        #endregion

        #region Database Loading

        private Dictionary<string, MaterialAcousticData> LoadMaterialDatabase()
        {
            return new Dictionary<string, MaterialAcousticData>(StringComparer.OrdinalIgnoreCase)
            {
                // Hard surfaces
                { "Concrete", new MaterialAcousticData {
                    Name = "Concrete",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.01 }, { 250, 0.01 }, { 500, 0.02 }, { 1000, 0.02 }, { 2000, 0.02 }, { 4000, 0.03 } }
                }},
                { "Brick", new MaterialAcousticData {
                    Name = "Brick",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.03 }, { 250, 0.03 }, { 500, 0.03 }, { 1000, 0.04 }, { 2000, 0.05 }, { 4000, 0.07 } }
                }},
                { "Glass", new MaterialAcousticData {
                    Name = "Glass",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.35 }, { 250, 0.25 }, { 500, 0.18 }, { 1000, 0.12 }, { 2000, 0.07 }, { 4000, 0.04 } }
                }},
                { "Plasterboard", new MaterialAcousticData {
                    Name = "Plasterboard",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.29 }, { 250, 0.10 }, { 500, 0.05 }, { 1000, 0.04 }, { 2000, 0.07 }, { 4000, 0.09 } }
                }},

                // Flooring
                { "Carpet", new MaterialAcousticData {
                    Name = "Carpet on Underlay",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.08 }, { 250, 0.24 }, { 500, 0.57 }, { 1000, 0.69 }, { 2000, 0.71 }, { 4000, 0.73 } }
                }},
                { "Vinyl", new MaterialAcousticData {
                    Name = "Vinyl Floor",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.02 }, { 250, 0.02 }, { 500, 0.03 }, { 1000, 0.03 }, { 2000, 0.03 }, { 4000, 0.02 } }
                }},
                { "Timber Floor", new MaterialAcousticData {
                    Name = "Timber Floor",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.15 }, { 250, 0.11 }, { 500, 0.10 }, { 1000, 0.07 }, { 2000, 0.06 }, { 4000, 0.07 } }
                }},

                // Acoustic treatments
                { "Acoustic Tile", new MaterialAcousticData {
                    Name = "Acoustic Ceiling Tile",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.25 }, { 250, 0.45 }, { 500, 0.75 }, { 1000, 0.90 }, { 2000, 0.85 }, { 4000, 0.80 } }
                }},
                { "Acoustic Panel", new MaterialAcousticData {
                    Name = "Fabric Acoustic Panel",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.10 }, { 250, 0.40 }, { 500, 0.80 }, { 1000, 0.95 }, { 2000, 0.90 }, { 4000, 0.85 } }
                }},
                { "Perforated Metal", new MaterialAcousticData {
                    Name = "Perforated Metal with Backing",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.45 }, { 250, 0.70 }, { 500, 0.85 }, { 1000, 0.75 }, { 2000, 0.60 }, { 4000, 0.50 } }
                }},

                // Soft furnishings
                { "Curtain Heavy", new MaterialAcousticData {
                    Name = "Heavy Curtain",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.14 }, { 250, 0.35 }, { 500, 0.55 }, { 1000, 0.72 }, { 2000, 0.70 }, { 4000, 0.65 } }
                }},
                { "Upholstered Seating", new MaterialAcousticData {
                    Name = "Upholstered Seating",
                    AbsorptionCoefficients = new Dictionary<int, double>
                    { { 125, 0.19 }, { 250, 0.37 }, { 500, 0.56 }, { 1000, 0.67 }, { 2000, 0.61 }, { 4000, 0.59 } }
                }}
            };
        }

        private Dictionary<string, RoomAcousticTarget> LoadRoomTargets()
        {
            return new Dictionary<string, RoomAcousticTarget>(StringComparer.OrdinalIgnoreCase)
            {
                { "Office Open Plan", new RoomAcousticTarget { MinRT60 = 0.4, MaxRT60 = 0.6, TargetSTI = 0.5, Notes = "Speech privacy important" } },
                { "Office Cellular", new RoomAcousticTarget { MinRT60 = 0.4, MaxRT60 = 0.6, TargetSTI = 0.6, Notes = "Concentration workspace" } },
                { "Meeting Room", new RoomAcousticTarget { MinRT60 = 0.4, MaxRT60 = 0.6, TargetSTI = 0.65, Notes = "Clear speech essential" } },
                { "Boardroom", new RoomAcousticTarget { MinRT60 = 0.5, MaxRT60 = 0.8, TargetSTI = 0.65, Notes = "Video conference quality" } },
                { "Classroom", new RoomAcousticTarget { MinRT60 = 0.4, MaxRT60 = 0.6, TargetSTI = 0.65, Notes = "BB93 compliance" } },
                { "Lecture Hall", new RoomAcousticTarget { MinRT60 = 0.6, MaxRT60 = 1.0, TargetSTI = 0.60, Notes = "PA system support" } },
                { "Library", new RoomAcousticTarget { MinRT60 = 0.6, MaxRT60 = 0.8, TargetSTI = 0.5, Notes = "Quiet study environment" } },
                { "Restaurant", new RoomAcousticTarget { MinRT60 = 0.5, MaxRT60 = 0.8, TargetSTI = 0.5, Notes = "Buzzy but intelligible" } },
                { "Auditorium", new RoomAcousticTarget { MinRT60 = 1.0, MaxRT60 = 1.5, TargetSTI = 0.55, Notes = "Unamplified speech" } },
                { "Concert Hall", new RoomAcousticTarget { MinRT60 = 1.8, MaxRT60 = 2.2, TargetSTI = 0.4, Notes = "Orchestral music" } },
                { "Hospital Ward", new RoomAcousticTarget { MinRT60 = 0.5, MaxRT60 = 0.8, TargetSTI = 0.6, Notes = "Speech + rest balance" } },
                { "Residential", new RoomAcousticTarget { MinRT60 = 0.4, MaxRT60 = 0.6, TargetSTI = 0.6, Notes = "Comfortable living" } },
                { "Worship", new RoomAcousticTarget { MinRT60 = 1.5, MaxRT60 = 2.5, TargetSTI = 0.5, Notes = "Congregation + music" } }
            };
        }

        #endregion
    }

    #region Data Models

    public class RoomGeometry
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Volume => Length * Width * Height;
        public double FloorArea => Length * Width;
        public double SurfaceArea => 2 * (Length * Width + Length * Height + Width * Height);
        public double Temperature { get; set; } = 20; // Celsius
        public double RelativeHumidity { get; set; } = 50; // Percent
    }

    public class SurfaceMaterial
    {
        public string SurfaceType { get; set; } // Ceiling, Wall, Floor
        public string MaterialName { get; set; }
        public double Area { get; set; }
    }

    public class MaterialAcousticData
    {
        public string Name { get; set; }
        public Dictionary<int, double> AbsorptionCoefficients { get; set; }
        public double NRC => (AbsorptionCoefficients.GetValueOrDefault(250, 0) +
                             AbsorptionCoefficients.GetValueOrDefault(500, 0) +
                             AbsorptionCoefficients.GetValueOrDefault(1000, 0) +
                             AbsorptionCoefficients.GetValueOrDefault(2000, 0)) / 4.0;
    }

    public class RoomAcousticTarget
    {
        public double MinRT60 { get; set; }
        public double MaxRT60 { get; set; }
        public double TargetSTI { get; set; }
        public string Notes { get; set; }
    }

    public class RoomAcousticAnalysis
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public double Volume { get; set; }
        public double FloorArea { get; set; }
        public Dictionary<int, double> AbsorptionByFrequency { get; set; }
        public Dictionary<int, double> RT60ByFrequency { get; set; }
        public double RT60Mid { get; set; }
        public RoomAcousticTarget TargetRT60 { get; set; }
        public bool MeetsTarget { get; set; }
        public double SpeechTransmissionIndex { get; set; }
        public string SpeechIntelligibility { get; set; }
        public double C50 { get; set; }
        public double C80 { get; set; }
        public List<AcousticRecommendation> Recommendations { get; set; }
    }

    public class PartitionAssembly
    {
        public string PartitionId { get; set; }
        public double Area { get; set; }
        public bool IsDoubleLeaf { get; set; }
        public double Mass1 { get; set; } // kg/m²
        public double Thickness1 { get; set; } // mm
        public double Mass2 { get; set; }
        public double Thickness2 { get; set; }
        public double CavityDepth { get; set; } // mm
        public bool HasCavityInsulation { get; set; }
        public double OverallRw { get; set; }
        public bool HasDoor { get; set; }
        public double DoorRw { get; set; }
        public bool HasPerimeterSeals { get; set; }
        public bool HasUnseakedPenetrations { get; set; }
        public bool HasBackToBackSockets { get; set; }
    }

    public class SoundTransmissionAnalysis
    {
        public string PartitionId { get; set; }
        public string SourceRoomId { get; set; }
        public string ReceiverRoomId { get; set; }
        public double Rw { get; set; }
        public double RwApparent { get; set; }
        public double DnTw { get; set; }
        public List<ComplianceResult> ComplianceResults { get; set; }
        public List<AcousticWeakPoint> WeakPoints { get; set; }
    }

    public class AcousticRequirement
    {
        public string Standard { get; set; }
        public string Description { get; set; }
        public double RequiredDnTw { get; set; }
    }

    public class ComplianceResult
    {
        public string Standard { get; set; }
        public string Requirement { get; set; }
        public double RequiredValue { get; set; }
        public double ActualValue { get; set; }
        public bool Compliant { get; set; }
        public double Margin { get; set; }
    }

    public class AcousticWeakPoint
    {
        public string Element { get; set; }
        public string Issue { get; set; }
        public string Severity { get; set; }
        public string Recommendation { get; set; }
    }

    public class AcousticRecommendation
    {
        public string Category { get; set; }
        public string Priority { get; set; }
        public string Issue { get; set; }
        public string Recommendation { get; set; }
        public string ExpectedImprovement { get; set; }
    }

    #endregion
}
