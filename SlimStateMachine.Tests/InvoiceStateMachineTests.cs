namespace SlimStateMachine.Tests
{
    [TestClass]
    public class InvoiceStateMachineTests
    {
        // Ensure configuration is cleared before each test method
        [TestInitialize]
        public void TestInitialize()
        {
            // Use internal method for test cleanup
            StateMachine<Invoice, InvoiceStatus>.ClearConfiguration_TestOnly();
        }

        private static void ConfigureInvoiceStateMachine(bool includeConditions = true)
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status, // The status property
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);

                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);

                    if (includeConditions)
                    {
                        builder.AllowTransition(
                            InvoiceStatus.Sent,
                            InvoiceStatus.Paid,
                            preCondition: inv => inv.RemainingAmount <= 0, // Must be fully paid
                            preConditionExpression: "Remaining == 0",     // For graph
                            postAction: inv => Console.WriteLine($"Invoice {inv.Id} marked as Paid.") // Example action
                        );
                        builder.AllowTransition(
                           InvoiceStatus.Draft,
                           InvoiceStatus.Cancelled,
                           postAction: inv => inv.CancellationReason = "Cancelled from Draft"
                       );
                        builder.AllowTransition(
                           InvoiceStatus.Sent,
                           InvoiceStatus.Cancelled,
                           preCondition: inv => !inv.RequiresApproval, // Cannot cancel if approval needed
                           preConditionExpression: "!RequiresApproval",
                           postAction: inv => inv.CancellationReason = "Cancelled after Sending"
                       );
                    }
                    else // Simplified config without conditions for some tests
                    {
                        builder.AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Paid);
                        builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Cancelled);
                        builder.AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Cancelled);
                    }
                    // Add a transition back for testing GetPossibleTransitions from Sent
                    builder.AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Draft);
                }
            );
        }

        [TestMethod]
        public void Configure_SetsInitialState()
        {
            ConfigureInvoiceStateMachine();
            Assert.AreEqual(InvoiceStatus.Draft, StateMachine<Invoice, InvoiceStatus>.InitialState);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Configure_ThrowsIfAlreadyConfigured()
        {
            ConfigureInvoiceStateMachine();
            ConfigureInvoiceStateMachine(); // Try to configure again
        }

        [TestMethod]
        [ExpectedException(typeof(StateMachineException))]
        public void Configure_ThrowsIfInitialStateNotSet()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
               invoice => invoice.Status,
               builder => {
                   // Missing builder.SetInitialState(...)
                   builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
               });
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GetInitialState_ThrowsIfNotConfigured()
        {
            _ = StateMachine<Invoice, InvoiceStatus>.InitialState;
        }

        [TestMethod]
        public void CanTransition_ValidTransition_NoCondition_ReturnsTrue()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Sent));
        }

        [TestMethod]
        public void CanTransition_InvalidTransition_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Paid)); // Draft -> Paid not defined
        }

        [TestMethod]
        public void CanTransition_WithPreCondition_Met_ReturnsTrue()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 100 }; // Remaining = 0
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Paid));
        }

        [TestMethod]
        public void CanTransition_WithPreCondition_NotMet_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50 }; // Remaining > 0
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Paid));
        }

        [TestMethod]
        public void CanTransition_WithPreCondition_Met_Inverted_ReturnsTrue()
        {
            ConfigureInvoiceStateMachine();
            // Sent -> Cancelled requires !RequiresApproval
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, RequiresApproval = false };
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Cancelled));
        }

        [TestMethod]
        public void CanTransition_WithPreCondition_NotMet_Inverted_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            // Sent -> Cancelled requires !RequiresApproval
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, RequiresApproval = true };
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Cancelled));
        }


        [TestMethod]
        public void TryTransition_ValidTransition_NoCondition_UpdatesState_ReturnsTrue()
        {
            ConfigureInvoiceStateMachine(false); // Use config without conditions for simplicity here
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent);

            Assert.IsTrue(result);
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);
        }

        [TestMethod]
        public void TryTransition_InvalidTransition_DoesNotUpdateState_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Paid); // Invalid transition

            Assert.IsFalse(result);
            Assert.AreEqual(InvoiceStatus.Draft, invoice.Status); // State unchanged
        }


        [TestMethod]
        public void TryTransition_WithPreCondition_Met_UpdatesState_ExecutesPostAction_ReturnsTrue()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 100 };
            string? reason = null;
            // Reconfigure slightly to capture post action effect
            StateMachine<Invoice, InvoiceStatus>.ClearConfiguration_TestOnly();
            StateMachine<Invoice, InvoiceStatus>.Configure(inv => inv.Status, b => {
                b.SetInitialState(InvoiceStatus.Draft);
                b.AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Paid, inv => inv.RemainingAmount <= 0, null, _ => reason = "Paid");
            });


            var result = StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Paid);

            Assert.IsTrue(result);
            Assert.AreEqual(InvoiceStatus.Paid, invoice.Status);
            Assert.AreEqual("Paid", reason); // Check post action effect
        }

        [TestMethod]
        public void TryTransition_WithPreCondition_NotMet_DoesNotUpdateState_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50 };
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Paid);

            Assert.IsFalse(result);
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status); // State unchanged
            Assert.IsNull(invoice.CancellationReason); // Post action should not run
        }

        [TestMethod]
        public void TryTransition_ExecutesPostActionOnSuccess()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Cancelled); // Draft -> Cancelled has post action

            Assert.IsTrue(result);
            Assert.AreEqual(InvoiceStatus.Cancelled, invoice.Status);
            Assert.AreEqual("Cancelled from Draft", invoice.CancellationReason); // Verify post action executed
        }


        [TestMethod]
        public void GetPossibleTransitions_ReturnsCorrectStates_BasedOnEntity()
        {
            ConfigureInvoiceStateMachine();
            // From Sent: Can go to Paid (if remaining=0), Cancelled (if !RequiresApproval), Draft
            var invoicePaid = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 100, RequiresApproval = true };
            var invoiceUnpaidNotReq = new Invoice { Id = 2, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50, RequiresApproval = false };
            var invoiceUnpaidReq = new Invoice { Id = 3, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50, RequiresApproval = true };

            var transitionsPaid = StateMachine<Invoice, InvoiceStatus>.GetPossibleTransitions(invoicePaid).ToList();
            var transitionsUnpaidNotReq = StateMachine<Invoice, InvoiceStatus>.GetPossibleTransitions(invoiceUnpaidNotReq).ToList();
            var transitionsUnpaidReq = StateMachine<Invoice, InvoiceStatus>.GetPossibleTransitions(invoiceUnpaidReq).ToList();

            // Invoice 1 (Paid, Requires Approval): Can go to Paid, Draft
            CollectionAssert.AreEquivalent(new[] { InvoiceStatus.Paid, InvoiceStatus.Draft }, transitionsPaid);

            // Invoice 2 (Unpaid, No Approval): Can go to Cancelled, Draft
            CollectionAssert.AreEquivalent(new[] { InvoiceStatus.Cancelled, InvoiceStatus.Draft }, transitionsUnpaidNotReq);

            // Invoice 3 (Unpaid, Requires Approval): Can only go to Draft
            CollectionAssert.AreEquivalent(new[] { InvoiceStatus.Draft }, transitionsUnpaidReq);
        }

        [TestMethod]
        public void GetDefinedTransitions_ReturnsAllDefinedTargets_IgnoringConditions()
        {
            ConfigureInvoiceStateMachine(); // Includes conditions

            // From Sent: Defined transitions are to Paid, Cancelled, Draft
            var definedFromSent = StateMachine<Invoice, InvoiceStatus>.GetDefinedTransitions(InvoiceStatus.Sent).ToList();

            CollectionAssert.AreEquivalent(new[] { InvoiceStatus.Paid, InvoiceStatus.Cancelled, InvoiceStatus.Draft }, definedFromSent);

            // From Draft: Defined transitions are to Sent, Cancelled
            var definedFromDraft = StateMachine<Invoice, InvoiceStatus>.GetDefinedTransitions(InvoiceStatus.Draft).ToList();
            CollectionAssert.AreEquivalent(new[] { InvoiceStatus.Sent, InvoiceStatus.Cancelled }, definedFromDraft);

            // From Paid: No defined outgoing transitions
            var definedFromPaid = StateMachine<Invoice, InvoiceStatus>.GetDefinedTransitions(InvoiceStatus.Paid).ToList();
            Assert.AreEqual(0, definedFromPaid.Count);
        }


        [TestMethod]
        public void GenerateMermaidGraph_OutputsCorrectFormat()
        {
            ConfigureInvoiceStateMachine(); // Use the one WITH conditions/expressions
            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateMermaidGraph();

            Console.WriteLine(graph); // Print for visual inspection during test runs

            // Basic structural checks
            Assert.IsTrue(graph.StartsWith("graph TD"));
            Assert.IsTrue(graph.Contains("Start((⚪)) --> Draft")); // Initial state
            Assert.IsTrue(graph.Contains("Draft --> Sent")); // Simple transition
            Assert.IsTrue(graph.Contains("Sent -- \"Remaining == 0\" --> Paid")); // Transition with condition label
            Assert.IsTrue(graph.Contains("Sent -- \"!RequiresApproval\" --> Cancelled")); // Transition with condition label
            Assert.IsTrue(graph.Contains("Draft --> Cancelled")); // Transition with post-action but no condition label
            Assert.IsTrue(graph.Contains("Sent --> Draft")); // The transition back

            // Check for escaped quotes if they were used in expression (not in this example, but good practice)
            // Assert.IsTrue(graph.Contains("SomeState -- \"Condition with #quot;quotes#quot;\" --> AnotherState"));
        }

        [TestMethod]
        public void GenerateMermaidGraph_HandlesNoTransitions()
        {
            // Configure only initial state
            StateMachine<Invoice, InvoiceStatus>.Configure(
               invoice => invoice.Status,
               builder => builder.SetInitialState(InvoiceStatus.Draft));

            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateMermaidGraph();
            Console.WriteLine(graph);

            Assert.IsTrue(graph.StartsWith("graph TD"));
            Assert.IsTrue(graph.Contains("Start((⚪)) --> Draft"));
            // Ensure Draft node exists even without transitions
            Assert.IsTrue(graph.TrimEnd().EndsWith("Draft") || graph.Contains("Draft\n")); // Might be just the initial link or a separate node def
        }

        [TestMethod]
        public void CheckFinalStates()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Paid };
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.IsInFinalState(invoice));
            invoice.Status = InvoiceStatus.Sent;
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.IsInFinalState(invoice));
            invoice.Status = InvoiceStatus.Cancelled;
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.IsInFinalState(invoice));

            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.IsFinalState(InvoiceStatus.Paid));
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.IsFinalState(InvoiceStatus.Sent));
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.IsFinalState(InvoiceStatus.Cancelled));
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.IsFinalState(InvoiceStatus.Draft));
        }

        [TestMethod]
        public void TryTransitionAny_FirstTransitionValid_TransitionsAndReturnsTrue()
        {
            ConfigureInvoiceStateMachine(false); // No conditions for simplicity
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice, [InvoiceStatus.Sent, InvoiceStatus.Paid, InvoiceStatus.Cancelled], out var actualState);

            Assert.IsTrue(result);
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);
            Assert.AreEqual(InvoiceStatus.Sent, actualState);
        }

        [TestMethod]
        public void TryTransitionAny_SecondTransitionValid_TransitionsAndReturnsTrue()
        {
            ConfigureInvoiceStateMachine(false); // No conditions for simplicity
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            // Draft -> Paid is invalid, Draft -> Sent is valid
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice, [InvoiceStatus.Paid, InvoiceStatus.Sent], out var actualState);

            Assert.IsTrue(result);
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);
            Assert.AreEqual(InvoiceStatus.Sent, actualState);
        }

        [TestMethod]
        public void TryTransitionAny_NoTransitionsValid_ReturnsFalseAndStateUnchanged()
        {
            ConfigureInvoiceStateMachine(false); // No conditions for simplicity
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Paid };

            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice, [InvoiceStatus.Sent, InvoiceStatus.Draft], out var actualState);

            Assert.IsFalse(result);
            Assert.AreEqual(InvoiceStatus.Paid, invoice.Status);
            Assert.AreEqual(default, actualState);
        }

        [TestMethod]
        public void TryTransitionAny_WithPreConditions_TransitionsToFirstValid()
        {
            ConfigureInvoiceStateMachine();
            // Sent -> Paid requires RemainingAmount == 0, Sent -> Cancelled requires !RequiresApproval
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 100, RequiresApproval = false };

            // Both transitions are valid, should take Paid first
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice, [InvoiceStatus.Paid, InvoiceStatus.Cancelled], out var actualState);

            Assert.IsTrue(result);
            Assert.AreEqual(InvoiceStatus.Paid, invoice.Status);
            Assert.AreEqual(InvoiceStatus.Paid, actualState);
        }

        [TestMethod]
        public void TryTransitionAny_WithPreConditions_TransitionsToSecondIfFirstNotMet()
        {
            ConfigureInvoiceStateMachine();
            // Sent -> Paid requires RemainingAmount == 0, Sent -> Cancelled requires !RequiresApproval
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50, RequiresApproval = false };

            // Paid not allowed (Remaining > 0), Cancelled allowed (!RequiresApproval)
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice, [InvoiceStatus.Paid, InvoiceStatus.Cancelled], out var actualState);

            Assert.IsTrue(result);
            Assert.AreEqual(InvoiceStatus.Cancelled, invoice.Status);
            Assert.AreEqual(InvoiceStatus.Cancelled, actualState);
        }

        [TestMethod]
        public void TryTransitionAny_WithPreConditions_NoneMet_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            // Sent -> Paid requires RemainingAmount == 0, Sent -> Cancelled requires !RequiresApproval
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50, RequiresApproval = true };

            // Neither transition allowed
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice, [InvoiceStatus.Paid, InvoiceStatus.Cancelled], out var actualState);

            Assert.IsFalse(result);
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);
            Assert.AreEqual(default, actualState);
        }

        [TestMethod]
        public void TryTransitionAny_EmptyTransitions_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine(false); // No conditions for simplicity
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice, [], out var actualState);
            Assert.IsFalse(result);
            Assert.AreEqual(InvoiceStatus.Draft, invoice.Status);
            Assert.AreEqual(default, actualState);
        }


        [TestMethod]
        public void TryTransitionAny()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft, TotalAmount = 100 };

            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice);
            Assert.IsTrue(result);

            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status); // First valid transition

            // Update payment to allow transition to Paid
            invoice.AmountPaid = 100;

            result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice);
            Assert.IsTrue(result);

            Assert.AreEqual(InvoiceStatus.Paid, invoice.Status); // Next valid transition
        }

        #region D2 Graph Generation Tests

        [TestMethod]
        public void GenerateD2Graph_OutputsCorrectFormat()
        {
            ConfigureInvoiceStateMachine();
            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph();

            Console.WriteLine(graph);

            // Basic structural checks
            Assert.IsTrue(graph.Contains("# State Machine: Invoice - InvoiceStatus"));
            Assert.IsTrue(graph.Contains("direction: down"));
            Assert.IsTrue(graph.Contains("Start -> Draft")); // Initial state
            Assert.IsTrue(graph.Contains("Draft -> Sent")); // Simple transition
            Assert.IsTrue(graph.Contains("Sent -> Paid: Remaining == 0")); // Transition with condition label
            Assert.IsTrue(graph.Contains("Sent -> Cancelled: !RequiresApproval")); // Transition with condition label
            Assert.IsTrue(graph.Contains("Draft -> Cancelled")); // Transition without condition
            Assert.IsTrue(graph.Contains("Sent -> Draft")); // The transition back
        }

        [TestMethod]
        public void GenerateD2Graph_IncludesStylesByDefault()
        {
            ConfigureInvoiceStateMachine();
            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph();

            Assert.IsTrue(graph.Contains("# Styles"));
            Assert.IsTrue(graph.Contains("style {"));
            Assert.IsTrue(graph.Contains("fill: honeydew"));
            Assert.IsTrue(graph.Contains("Start: {"));
            Assert.IsTrue(graph.Contains("shape: circle"));
        }

        [TestMethod]
        public void GenerateD2Graph_WithoutStyles()
        {
            ConfigureInvoiceStateMachine();
            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph(includeStyles: false);

            Console.WriteLine(graph);

            Assert.IsFalse(graph.Contains("# Styles"));
            Assert.IsFalse(graph.Contains("style {"));
            Assert.IsTrue(graph.Contains("Start -> Draft")); // Still has transitions
        }

        [TestMethod]
        public void GenerateD2Graph_WithEntity_HighlightsCurrentState()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent };
            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph(invoice);

            Console.WriteLine(graph);

            Assert.IsTrue(graph.Contains("# Current State: Sent"));
            Assert.IsTrue(graph.Contains("Sent: {"));
            Assert.IsTrue(graph.Contains("style.fill: lightyellow"));
            Assert.IsTrue(graph.Contains("style.stroke: orange"));
        }

        [TestMethod]
        public void GenerateD2Graph_WithExplicitState_HighlightsState()
        {
            ConfigureInvoiceStateMachine();
            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph(InvoiceStatus.Paid);

            Console.WriteLine(graph);

            Assert.IsTrue(graph.Contains("# Current State: Paid"));
            Assert.IsTrue(graph.Contains("Paid: {"));
            Assert.IsTrue(graph.Contains("style.fill: lightyellow"));
        }

        [TestMethod]
        public void GenerateD2Graph_HandlesNoTransitions()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
               invoice => invoice.Status,
               builder => builder.SetInitialState(InvoiceStatus.Draft));

            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph();
            Console.WriteLine(graph);

            Assert.IsTrue(graph.Contains("Start -> Draft"));
            Assert.IsTrue(graph.Contains("Draft")); // Initial state shown even without transitions
        }

        #endregion

        #region GenerateDiagram with DiagramType Tests

        [TestMethod]
        public void GenerateDiagram_Mermaid_ReturnsCorrectFormat()
        {
            ConfigureInvoiceStateMachine();
            var diagram = StateMachine<Invoice, InvoiceStatus>.GenerateDiagram(StateMachine<Invoice, InvoiceStatus>.DiagramType.Mermaid);

            Assert.IsTrue(diagram.StartsWith("graph TD"));
            Assert.IsTrue(diagram.Contains("Start((⚪)) --> Draft"));
        }

        [TestMethod]
        public void GenerateDiagram_D2_ReturnsCorrectFormat()
        {
            ConfigureInvoiceStateMachine();
            var diagram = StateMachine<Invoice, InvoiceStatus>.GenerateDiagram(StateMachine<Invoice, InvoiceStatus>.DiagramType.D2);

            Assert.IsTrue(diagram.Contains("# State Machine: Invoice - InvoiceStatus"));
            Assert.IsTrue(diagram.Contains("Start -> Draft"));
        }

        [TestMethod]
        public void GenerateDiagram_WithEntity_Mermaid()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent };
            var diagram = StateMachine<Invoice, InvoiceStatus>.GenerateDiagram(StateMachine<Invoice, InvoiceStatus>.DiagramType.Mermaid, invoice);

            Assert.IsTrue(diagram.Contains("style Sent fill:#ffffaa"));
        }

        [TestMethod]
        public void GenerateDiagram_WithEntity_D2()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent };
            var diagram = StateMachine<Invoice, InvoiceStatus>.GenerateDiagram(StateMachine<Invoice, InvoiceStatus>.DiagramType.D2, invoice);

            Assert.IsTrue(diagram.Contains("# Current State: Sent"));
        }

        [TestMethod]
        public void GenerateDiagram_WithExplicitState_Mermaid()
        {
            ConfigureInvoiceStateMachine();
            var diagram = StateMachine<Invoice, InvoiceStatus>.GenerateDiagram(StateMachine<Invoice, InvoiceStatus>.DiagramType.Mermaid, InvoiceStatus.Draft);

            Assert.IsTrue(diagram.Contains("style Draft fill:#ffffaa"));
        }

        [TestMethod]
        public void GenerateDiagram_WithExplicitState_D2()
        {
            ConfigureInvoiceStateMachine();
            var diagram = StateMachine<Invoice, InvoiceStatus>.GenerateDiagram(StateMachine<Invoice, InvoiceStatus>.DiagramType.D2, InvoiceStatus.Draft);

            Assert.IsTrue(diagram.Contains("# Current State: Draft"));
        }

        #endregion

        #region Mermaid Graph Highlighting Tests

        [TestMethod]
        public void GenerateMermaidGraph_WithEntity_HighlightsCurrentState()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent };
            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateMermaidGraph(invoice);

            Console.WriteLine(graph);

            Assert.IsTrue(graph.Contains("%% Styling for current state"));
            Assert.IsTrue(graph.Contains("style Sent fill:#ffffaa,stroke:#ffaa00,stroke-width:3px"));
        }

        [TestMethod]
        public void GenerateMermaidGraph_WithExplicitState_HighlightsState()
        {
            ConfigureInvoiceStateMachine();
            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateMermaidGraph(InvoiceStatus.Cancelled);

            Console.WriteLine(graph);

            Assert.IsTrue(graph.Contains("%% Styling for current state"));
            Assert.IsTrue(graph.Contains("style Cancelled fill:#ffffaa,stroke:#ffaa00,stroke-width:3px"));
        }

        #endregion

        #region CanTransition Overload Tests

        [TestMethod]
        public void CanTransition_WithExplicitFromState_ValidTransition_ReturnsTrue()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            // Check transition from Sent to Draft (even though entity is in Draft state)
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Sent, InvoiceStatus.Draft));
        }

        [TestMethod]
        public void CanTransition_WithExplicitFromState_InvalidTransition_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            // Draft -> Paid is not defined
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Draft, InvoiceStatus.Paid));
        }

        [TestMethod]
        public void CanTransition_WithExplicitFromState_PreConditionNotMet_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft, TotalAmount = 100, AmountPaid = 50 };

            // Sent -> Paid requires RemainingAmount <= 0
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Sent, InvoiceStatus.Paid));
        }

        [TestMethod]
        public void CanTransition_WithExplicitFromState_PreConditionMet_ReturnsTrue()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft, TotalAmount = 100, AmountPaid = 100 };

            // Sent -> Paid requires RemainingAmount <= 0
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Sent, InvoiceStatus.Paid));
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Configure_NullStatusPropertyAccessor_ThrowsArgumentNullException()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                null!,
                builder => builder.SetInitialState(InvoiceStatus.Draft));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Configure_NullConfigureAction_ThrowsArgumentNullException()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                null!);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Configure_InvalidExpression_NotProperty_ThrowsArgumentException()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.GetHashCode() == 0 ? InvoiceStatus.Draft : InvoiceStatus.Sent,
                builder => builder.SetInitialState(InvoiceStatus.Draft));
        }

        [TestMethod]
        [ExpectedException(typeof(StateMachineException))]
        public void TryTransition_PostActionThrows_WrapsInStateMachineException()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(
                        InvoiceStatus.Draft,
                        InvoiceStatus.Sent,
                        postAction: _ => throw new InvalidOperationException("Post action failed"));
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent);
        }

        [TestMethod]
        public void TryTransition_PostActionThrows_ExceptionContainsOriginalMessage()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(
                        InvoiceStatus.Draft,
                        InvoiceStatus.Sent,
                        postAction: _ => throw new InvalidOperationException("Original error message"));
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            try
            {
                StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent);
                Assert.Fail("Expected StateMachineException");
            }
            catch (StateMachineException ex)
            {
                Assert.IsTrue(ex.Message.Contains("Draft"));
                Assert.IsTrue(ex.Message.Contains("Sent"));
                Assert.IsTrue(ex.Message.Contains("Original error message"));
                Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidOperationException));
            }
        }

        #endregion

        #region Configuration Validation Tests

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AllowTransition_DuplicateTransition_ThrowsInvalidOperationException()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent); // Duplicate
                });
        }

        [TestMethod]
        public void AllowTransition_SameFromToDifferentTo_Allowed()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Cancelled);
                });

            var transitions = StateMachine<Invoice, InvoiceStatus>.GetDefinedTransitions(InvoiceStatus.Draft).ToList();
            Assert.AreEqual(2, transitions.Count);
            CollectionAssert.Contains(transitions, InvoiceStatus.Sent);
            CollectionAssert.Contains(transitions, InvoiceStatus.Cancelled);
        }

        #endregion
    }
}