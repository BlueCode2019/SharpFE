﻿//-----------------------------------------------------------------------
// <copyright file="LinearSolver.cs" company="SharpFE">
//     Copyright Iain Sproat, 2012.
// </copyright>
//-----------------------------------------------------------------------

namespace SharpFE.Solvers
{
    using System;
    using SharpFE.Stiffness;

    /// <summary>
    /// Carries out a simple, linear analysis to solve a static, implicit finite element problem.
    /// </summary>
    public abstract class LinearSolver : IFiniteElementSolver
    {
        /// <summary>
        /// The model with the data to solve
        /// </summary>
        protected FiniteElementModel model;
        
        /// <summary>
        /// A helper class to build the stiffness matrices from the model
        /// </summary>
        protected GlobalModelStiffnessMatrixBuilder matrixBuilder;
        
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="LinearSolver" /> class.
        /// </summary>
        /// <param name="modelToSolve">The model on which to run the analysis</param>
        public LinearSolver(FiniteElementModel modelToSolve)
            : this(modelToSolve, new GlobalModelStiffnessMatrixBuilder(modelToSolve))
        {
            // empty
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="LinearSolver" /> class.
        /// </summary>
        /// <param name="modelToSolve">The model on which to run the analysis</param>
        /// <param name="stiffnessMatrixBuilder">The class which generates the stiffness matrices for solving.</param>
        public LinearSolver(FiniteElementModel modelToSolve, GlobalModelStiffnessMatrixBuilder stiffnessMatrixBuilder)
        {
            Guard.AgainstNullArgument(modelToSolve, "modelToSolve");
            Guard.AgainstNullArgument(stiffnessMatrixBuilder, "stiffnessMatrixBuilder");
            
            this.model = modelToSolve;
            this.matrixBuilder = stiffnessMatrixBuilder;
        }
        #endregion
        
        /// <summary>
        /// Solves the model containing the finite element problem
        /// </summary>
        /// <returns>The results of analysis</returns>
        public FiniteElementResults Solve()
        {
            this.ThrowIfNotAValidModel();
            
            KeyedVector<NodalDegreeOfFreedom> displacements = this.CalculateUnknownDisplacements();
            KeyedVector<NodalDegreeOfFreedom> reactions = this.CalculateUnknownReactions(displacements);
            
            reactions = this.CombineExternalForcesOnReactionNodesWithReactions(reactions);
            
            return this.CreateResults(displacements, reactions);
        }
        
        /// <summary>
        /// Solves AX=B for X.
        /// </summary>
        /// <param name="stiffnessMatrix">The stiffness matrix</param>
        /// <param name="forceVector">The forces</param>
        /// <returns></returns>
        protected abstract KeyedVector<NodalDegreeOfFreedom> Solve(StiffnessMatrix stiffnessMatrix, KeyedVector<NodalDegreeOfFreedom> forceVector);
        
        /// <summary>
        /// Checks as to whether the model is valid for solving.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the model is invalid for solving.</exception>
        protected void ThrowIfNotAValidModel()
        {
            Guard.AgainstInvalidState(() => { return this.model.NodeCount < 2; },
                                      "The model has less than 2 nodes and so cannot be solved");
            
            Guard.AgainstInvalidState(() => { return this.model.ElementCount < 1; },
                                      "The model has no elements and so cannot be solved");
        }
        
        /// <summary>
        /// Calculates part of the problem for unknown displacements
        /// </summary>
        /// <returns>A vector of the displacements which were previously unknown, and have now been solved</returns>
        protected KeyedVector<NodalDegreeOfFreedom> CalculateUnknownDisplacements()
        {
            StiffnessMatrix knownForcesUnknownDisplacementStiffnesses = this.matrixBuilder.BuildKnownForcesUnknownDisplacementStiffnessMatrix(); // K11
            
            //TODO calculating the determinant is computationally intensive.  We should use another method of model verification to speed this up.
            double det = knownForcesUnknownDisplacementStiffnesses.Determinant();
            Guard.AgainstInvalidState(() => { return det.IsApproximatelyEqualTo(0.0); },
                                      "We are unable to solve this model as it is able to move as a rigid body without deforming in any way.  Are you missing any constraints?\r\nMatrix of stiffnesses for known forces and unknown displacements:\r\n {0}",
                                      knownForcesUnknownDisplacementStiffnesses);
            
            KeyedVector<NodalDegreeOfFreedom> knownForces = this.model.KnownForceVector(); // Fk
            StiffnessMatrix knownForcesKnownDisplacementsStiffnesses = this.matrixBuilder.BuildKnownForcesKnownDisplacementStiffnessMatrix(); // K12
            KeyedVector<NodalDegreeOfFreedom> knownDisplacements = this.model.KnownDisplacementVector(); // Uk
            
            // solve for unknown displacements
            // Uu = K11^-1 * (Fk + (K12 * Uk))
            KeyedVector<NodalDegreeOfFreedom> forcesDueToExternallyAppliedDisplacements = knownForcesKnownDisplacementsStiffnesses.Multiply(knownDisplacements); // K12 * Uk
            KeyedVector<NodalDegreeOfFreedom> externallyAppliedForces = knownForces.Add(forcesDueToExternallyAppliedDisplacements); // Fk + (K12 * Uk)
            
            // K11^-1 * (Fk + (K12 * Uk))
            KeyedVector<NodalDegreeOfFreedom> unknownDisplacements = this.Solve(knownForcesUnknownDisplacementStiffnesses, externallyAppliedForces);
            
            return unknownDisplacements;
        }
        
        /// <summary>
        /// Calculates part of the stiffness equations for the unknown reactions.
        /// </summary>
        /// <param name="unknownDisplacements">A vector of the displacements which were previously unknown</param>
        /// <returns>A vector of the reactions which were previously unknown, and have now been solved</returns>
        protected KeyedVector<NodalDegreeOfFreedom> CalculateUnknownReactions(KeyedVector<NodalDegreeOfFreedom> unknownDisplacements)
        {
            Guard.AgainstNullArgument(unknownDisplacements, "unknownDisplacements");
            
            // Fu = K21 * Uu + K22 * Uk
            StiffnessMatrix unknownForcesUnknownDisplacementStiffnesses = this.matrixBuilder.BuildUnknownForcesUnknownDisplacementStiffnessMatrix(); // K21
            StiffnessMatrix unknownForcesKnownDisplacementsStiffnesses = this.matrixBuilder.BuildUnknownForcesKnownDisplacementStiffnessMatrix(); // K22
            KeyedVector<NodalDegreeOfFreedom> knownDisplacements = this.model.KnownDisplacementVector(); // Uk
            
            KeyedVector<NodalDegreeOfFreedom> lhsStatement = unknownForcesUnknownDisplacementStiffnesses.Multiply(unknownDisplacements); // K21 * Uu
            KeyedVector<NodalDegreeOfFreedom> rhsStatement = unknownForcesKnownDisplacementsStiffnesses.Multiply(knownDisplacements); // K22 * Uk
            
            KeyedVector<NodalDegreeOfFreedom> unknownReactions = lhsStatement.Add(rhsStatement);
            return unknownReactions;
        }
        
        /// <summary>
        /// The user may have placed reactions directly on to fixed supports.
        /// These are ignored during the calculation, but the correct answer for the total reaction must include them
        /// </summary>
        /// <param name="reactions">The calculated values of the reactions</param>
        /// <returns>The calculated values of the reactions with additional external forces added where applicable.</returns>
        protected KeyedVector<NodalDegreeOfFreedom> CombineExternalForcesOnReactionNodesWithReactions(KeyedVector<NodalDegreeOfFreedom> reactions)
        {
            KeyedVector<NodalDegreeOfFreedom> externalForcesOnReactionNodes = this.model.GetCombinedForcesFor(reactions.Keys);
            return reactions.Add(externalForcesOnReactionNodes);
        }
        
        /// <summary>
        /// Puts the data into the results data structure.
        /// </summary>
        /// <param name="displacements">The calculated displacements.  The index of the values in the vector matches the index of the displacement identifiers.</param>
        /// <param name="reactions">The calculated reactions.  The index of the values in the vector matches the index of the reaction identifiers.</param>
        /// <returns>The results in a presentable data structure</returns>
        protected FiniteElementResults CreateResults(KeyedVector<NodalDegreeOfFreedom> displacements, KeyedVector<NodalDegreeOfFreedom> reactions)
        {
            Guard.AgainstNullArgument(displacements, "displacements");
            Guard.AgainstNullArgument(reactions, "reactions");
            
            FiniteElementResults results = new FiniteElementResults(this.model.ModelType);
            results.AddMultipleDisplacements(displacements);
            results.AddMultipleReactions(reactions);
            return results;
        }
    }
}
