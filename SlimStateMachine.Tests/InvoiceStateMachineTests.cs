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
            StateMachine<Invoice, InvoiceStatus>.Reset();
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
            StateMachine<Invoice, InvoiceStatus>.Reset();
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

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Configure_ReadOnlyProperty_ThrowsArgumentException()
        {
            StateMachine<ReadOnlyInvoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder => builder.SetInitialState(InvoiceStatus.Draft));
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void SelfLoop_TransitionToSameState_Works()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Draft); // Self-loop
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Draft));
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Draft));
            Assert.AreEqual(InvoiceStatus.Draft, invoice.Status);
        }

        [TestMethod]
        public void SelfLoop_WithPreCondition_RespectsCondition()
        {
            int transitionCount = 0;
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(
                        InvoiceStatus.Draft,
                        InvoiceStatus.Draft,
                        preCondition: inv => inv.TotalAmount > 0,
                        postAction: _ => transitionCount++);
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft, TotalAmount = 0 };
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Draft));
            Assert.AreEqual(0, transitionCount);

            invoice.TotalAmount = 100;
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Draft));
            Assert.AreEqual(1, transitionCount);
        }

        [TestMethod]
        public void NullEntity_CanTransition_ThrowsNullReferenceException()
        {
            ConfigureInvoiceStateMachine();
            Invoice? invoice = null;

            Assert.ThrowsException<NullReferenceException>(() =>
                StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice!, InvoiceStatus.Sent));
        }

        [TestMethod]
        public void NullEntity_TryTransition_ThrowsNullReferenceException()
        {
            ConfigureInvoiceStateMachine();
            Invoice? invoice = null;

            Assert.ThrowsException<NullReferenceException>(() =>
                StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice!, InvoiceStatus.Sent));
        }

        [TestMethod]
        public void NullEntity_GetPossibleTransitions_ThrowsNullReferenceException()
        {
            ConfigureInvoiceStateMachine();
            Invoice? invoice = null;

            Assert.ThrowsException<NullReferenceException>(() =>
                StateMachine<Invoice, InvoiceStatus>.GetPossibleTransitions(invoice!).ToList());
        }

        [TestMethod]
        public void NullEntity_IsInFinalState_ThrowsNullReferenceException()
        {
            ConfigureInvoiceStateMachine();
            Invoice? invoice = null;

            Assert.ThrowsException<NullReferenceException>(() =>
                StateMachine<Invoice, InvoiceStatus>.IsInFinalState(invoice!));
        }

        [TestMethod]
        public void GetPossibleTransitions_FromFinalState_ReturnsEmpty()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Paid }; // Paid is a final state

            var transitions = StateMachine<Invoice, InvoiceStatus>.GetPossibleTransitions(invoice).ToList();

            Assert.AreEqual(0, transitions.Count);
        }

        [TestMethod]
        public void GetDefinedTransitions_FromFinalState_ReturnsEmpty()
        {
            ConfigureInvoiceStateMachine();

            var transitions = StateMachine<Invoice, InvoiceStatus>.GetDefinedTransitions(InvoiceStatus.Paid).ToList();

            Assert.AreEqual(0, transitions.Count);
        }

        [TestMethod]
        public void TryTransition_AlreadyInTargetState_NoTransitionDefined_ReturnsFalse()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent };

            // No self-loop defined for Sent, so this should return false
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent));
        }

        #endregion

        #region Multi-State Machine Isolation Tests

        [TestMethod]
        public void DifferentEntityTypes_HaveIndependentConfigurations()
        {
            // Configure Invoice state machine
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                });

            // Configure Order state machine (different entity/enum)
            StateMachine<Order, OrderStatus>.Configure(
                order => order.Status,
                builder =>
                {
                    builder.SetInitialState(OrderStatus.Created);
                    builder.AllowTransition(OrderStatus.Created, OrderStatus.Processing);
                    builder.AllowTransition(OrderStatus.Processing, OrderStatus.Shipped);
                });

            // Verify they have different initial states
            Assert.AreEqual(InvoiceStatus.Draft, StateMachine<Invoice, InvoiceStatus>.InitialState);
            Assert.AreEqual(OrderStatus.Created, StateMachine<Order, OrderStatus>.InitialState);

            // Verify transitions work independently
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var order = new Order { Id = 1, Status = OrderStatus.Created };

            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent));
            Assert.IsTrue(StateMachine<Order, OrderStatus>.TryTransition(order, OrderStatus.Processing));

            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);
            Assert.AreEqual(OrderStatus.Processing, order.Status);

            // Verify defined transitions are independent
            var invoiceTransitions = StateMachine<Invoice, InvoiceStatus>.GetDefinedTransitions(InvoiceStatus.Draft).ToList();
            var orderTransitions = StateMachine<Order, OrderStatus>.GetDefinedTransitions(OrderStatus.Created).ToList();

            Assert.AreEqual(1, invoiceTransitions.Count);
            Assert.AreEqual(1, orderTransitions.Count);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Clean up Order state machine after tests that use it
            StateMachine<Order, OrderStatus>.Reset();
            StateMachine<ReadOnlyInvoice, InvoiceStatus>.Reset();
        }

        #endregion

        #region Builder Fluent API Tests

        [TestMethod]
        public void SetInitialState_ReturnsBuilder_ForChaining()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    var returnedBuilder = builder.SetInitialState(InvoiceStatus.Draft);
                    Assert.AreSame(builder, returnedBuilder);
                });
        }

        [TestMethod]
        public void AllowTransition_ReturnsBuilder_ForChaining()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    var returnedBuilder = builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                    Assert.AreSame(builder, returnedBuilder);
                });
        }

        [TestMethod]
        public void FluentChaining_ConfiguresCorrectly()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder => builder
                    .SetInitialState(InvoiceStatus.Draft)
                    .AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent)
                    .AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Paid)
                    .AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Cancelled));

            Assert.AreEqual(InvoiceStatus.Draft, StateMachine<Invoice, InvoiceStatus>.InitialState);

            var fromDraft = StateMachine<Invoice, InvoiceStatus>.GetDefinedTransitions(InvoiceStatus.Draft).ToList();
            var fromSent = StateMachine<Invoice, InvoiceStatus>.GetDefinedTransitions(InvoiceStatus.Sent).ToList();

            Assert.AreEqual(1, fromDraft.Count);
            Assert.AreEqual(2, fromSent.Count);
        }

        #endregion

        #region Diagram Edge Case Tests

        [TestMethod]
        public void GenerateDiagram_InvalidDiagramType_ThrowsArgumentException()
        {
            ConfigureInvoiceStateMachine();

            var invalidType = (StateMachine<Invoice, InvoiceStatus>.DiagramType)999;

            Assert.ThrowsException<ArgumentException>(() =>
                StateMachine<Invoice, InvoiceStatus>.GenerateDiagram(invalidType));
        }

        [TestMethod]
        public void GenerateMermaidGraph_PreConditionWithQuotes_EscapesCorrectly()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(
                        InvoiceStatus.Draft,
                        InvoiceStatus.Sent,
                        preCondition: inv => inv.CustomerName != null,
                        preConditionExpression: "Name != \"empty\"");
                });

            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateMermaidGraph();
            Console.WriteLine(graph);

            // Quotes should be escaped as #quot;
            Assert.IsTrue(graph.Contains("#quot;"));
            Assert.IsFalse(graph.Contains("\"empty\"")); // Raw quotes should not appear in the label
        }

        [TestMethod]
        public void GenerateD2Graph_PreConditionWithSpecialChars_IncludedAsIs()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(
                        InvoiceStatus.Draft,
                        InvoiceStatus.Sent,
                        preCondition: inv => inv.TotalAmount > 0,
                        preConditionExpression: "Amount > 0 && Valid");
                });

            var graph = StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph();
            Console.WriteLine(graph);

            Assert.IsTrue(graph.Contains("Amount > 0 && Valid"));
        }

        #endregion

        #region State Property Edge Case Tests

        [TestMethod]
        public void EntityState_DoesNotMatchInitialState_TransitionsFromActualState()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                    builder.AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Paid);
                });

            // Entity starts in Sent (not the configured initial state Draft)
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent };

            // Should NOT be able to transition to Sent (Draft -> Sent, but we're in Sent)
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Sent));

            // SHOULD be able to transition to Paid (Sent -> Paid)
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Paid));
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Paid));
            Assert.AreEqual(InvoiceStatus.Paid, invoice.Status);
        }

        [TestMethod]
        public void EntityState_DefaultEnumValue_WorksCorrectly()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Sent); // NOT the default enum value (Draft = 0)
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                    builder.AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Paid);
                });

            // New entity has default enum value (Draft = 0), not the configured initial state
            var invoice = new Invoice { Id = 1 }; // Status defaults to Draft (0)

            Assert.AreEqual(InvoiceStatus.Draft, invoice.Status);
            Assert.AreEqual(InvoiceStatus.Sent, StateMachine<Invoice, InvoiceStatus>.InitialState);

            // Can transition from actual state (Draft), not from InitialState
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Sent));
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.CanTransition(invoice, InvoiceStatus.Paid));
        }

        [TestMethod]
        public void TryTransitionAny_NoArgument_FromFinalState_ReturnsFalse()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Paid }; // Final state

            var result = StateMachine<Invoice, InvoiceStatus>.TryTransitionAny(invoice);

            Assert.IsFalse(result);
            Assert.AreEqual(InvoiceStatus.Paid, invoice.Status);
        }

        #endregion

        #region OnEntry/OnExit Tests

        [TestMethod]
        public void OnEntry_ExecutedAfterStateChange()
        {
            InvoiceStatus? stateWhenOnEntryCalled = null;

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                    builder.OnEntry(InvoiceStatus.Sent, inv => stateWhenOnEntryCalled = inv.Status);
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent);

            Assert.AreEqual(InvoiceStatus.Sent, stateWhenOnEntryCalled);
        }

        [TestMethod]
        public void OnExit_ExecutedBeforeStateChange()
        {
            InvoiceStatus? stateWhenOnExitCalled = null;

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                    builder.OnExit(InvoiceStatus.Draft, inv => stateWhenOnExitCalled = inv.Status);
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent);

            // OnExit is called before state change, so it should see Draft
            Assert.AreEqual(InvoiceStatus.Draft, stateWhenOnExitCalled);
        }

        [TestMethod]
        public void OnEntry_OnExit_BothExecutedInOrder()
        {
            var executionOrder = new List<string>();

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent,
                        postAction: _ => executionOrder.Add("PostAction"));
                    builder.OnExit(InvoiceStatus.Draft, _ => executionOrder.Add("OnExit"));
                    builder.OnEntry(InvoiceStatus.Sent, _ => executionOrder.Add("OnEntry"));
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent);

            Assert.AreEqual(3, executionOrder.Count);
            Assert.AreEqual("PostAction", executionOrder[0]);
            Assert.AreEqual("OnExit", executionOrder[1]);
            Assert.AreEqual("OnEntry", executionOrder[2]);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void OnEntry_DuplicateRegistration_ThrowsInvalidOperationException()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.OnEntry(InvoiceStatus.Sent, _ => { });
                    builder.OnEntry(InvoiceStatus.Sent, _ => { }); // Duplicate
                });
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void OnExit_DuplicateRegistration_ThrowsInvalidOperationException()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.OnExit(InvoiceStatus.Draft, _ => { });
                    builder.OnExit(InvoiceStatus.Draft, _ => { }); // Duplicate
                });
        }

        #endregion

        #region OnTransition Event Tests

        [TestMethod]
        public void OnTransition_EventRaisedAfterSuccessfulTransition()
        {
            TransitionContext<Invoice, InvoiceStatus>? capturedContext = null;

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                });

            StateMachine<Invoice, InvoiceStatus>.OnTransition += ctx => capturedContext = ctx;

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent);

            Assert.IsNotNull(capturedContext);
            Assert.AreSame(invoice, capturedContext.Entity);
            Assert.AreEqual(InvoiceStatus.Draft, capturedContext.FromState);
            Assert.AreEqual(InvoiceStatus.Sent, capturedContext.ToState);
            Assert.IsFalse(capturedContext.WasForced);
        }

        [TestMethod]
        public void OnTransition_NotRaisedOnFailedTransition()
        {
            bool eventRaised = false;

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent,
                        preCondition: _ => false); // Always fails
                });

            StateMachine<Invoice, InvoiceStatus>.OnTransition += _ => eventRaised = true;

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent);

            Assert.IsFalse(eventRaised);
        }

        [TestMethod]
        public void OnTransition_IncludesReasonAndMetadata()
        {
            TransitionContext<Invoice, InvoiceStatus>? capturedContext = null;

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                });

            StateMachine<Invoice, InvoiceStatus>.OnTransition += ctx => capturedContext = ctx;

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var metadata = new Dictionary<string, object> { ["UserId"] = 123, ["Source"] = "API" };
            StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent, "Customer requested", metadata);

            Assert.IsNotNull(capturedContext);
            Assert.AreEqual("Customer requested", capturedContext.Reason);
            Assert.IsNotNull(capturedContext.Metadata);
            Assert.AreEqual(123, capturedContext.Metadata["UserId"]);
            Assert.AreEqual("API", capturedContext.Metadata["Source"]);
        }

        #endregion

        #region GetAllStates/GetAllTransitions Tests

        [TestMethod]
        public void GetAllStates_ReturnsAllEnumValues()
        {
            ConfigureInvoiceStateMachine();

            var allStates = StateMachine<Invoice, InvoiceStatus>.GetAllStates();

            Assert.AreEqual(4, allStates.Length);
            CollectionAssert.Contains(allStates, InvoiceStatus.Draft);
            CollectionAssert.Contains(allStates, InvoiceStatus.Sent);
            CollectionAssert.Contains(allStates, InvoiceStatus.Paid);
            CollectionAssert.Contains(allStates, InvoiceStatus.Cancelled);
        }

        [TestMethod]
        public void GetAllTransitions_ReturnsCompleteTransitionMap()
        {
            ConfigureInvoiceStateMachine();

            var transitions = StateMachine<Invoice, InvoiceStatus>.GetAllTransitions();

            Assert.IsTrue(transitions.ContainsKey(InvoiceStatus.Draft));
            Assert.IsTrue(transitions.ContainsKey(InvoiceStatus.Sent));

            // Draft can go to Sent or Cancelled
            CollectionAssert.Contains(transitions[InvoiceStatus.Draft], InvoiceStatus.Sent);
            CollectionAssert.Contains(transitions[InvoiceStatus.Draft], InvoiceStatus.Cancelled);

            // Sent can go to Paid, Cancelled, or Draft
            CollectionAssert.Contains(transitions[InvoiceStatus.Sent], InvoiceStatus.Paid);
            CollectionAssert.Contains(transitions[InvoiceStatus.Sent], InvoiceStatus.Cancelled);
            CollectionAssert.Contains(transitions[InvoiceStatus.Sent], InvoiceStatus.Draft);
        }

        #endregion

        #region ForceTransition Tests

        [TestMethod]
        public void ForceTransition_BypassesPreCondition()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent,
                        preCondition: _ => false); // Always fails
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            // Normal transition fails
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent));
            Assert.AreEqual(InvoiceStatus.Draft, invoice.Status);

            // Force transition succeeds
            Assert.IsTrue(StateMachine<Invoice, InvoiceStatus>.ForceTransition(invoice, InvoiceStatus.Sent));
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);
        }

        [TestMethod]
        public void ForceTransition_StillRequiresDefinedTransition()
        {
            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            // Cannot force transition that isn't defined
            Assert.IsFalse(StateMachine<Invoice, InvoiceStatus>.ForceTransition(invoice, InvoiceStatus.Paid));
            Assert.AreEqual(InvoiceStatus.Draft, invoice.Status);
        }

        [TestMethod]
        public void ForceTransition_SetsWasForcedInContext()
        {
            TransitionContext<Invoice, InvoiceStatus>? capturedContext = null;

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent,
                        preCondition: _ => false);
                });

            StateMachine<Invoice, InvoiceStatus>.OnTransition += ctx => capturedContext = ctx;

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            StateMachine<Invoice, InvoiceStatus>.ForceTransition(invoice, InvoiceStatus.Sent, "Admin override");

            Assert.IsNotNull(capturedContext);
            Assert.IsTrue(capturedContext.WasForced);
            Assert.AreEqual("Admin override", capturedContext.Reason);
        }

        [TestMethod]
        public void ForceTransition_ExecutesOnEntryOnExit()
        {
            var executionOrder = new List<string>();

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent,
                        preCondition: _ => false);
                    builder.OnExit(InvoiceStatus.Draft, _ => executionOrder.Add("OnExit"));
                    builder.OnEntry(InvoiceStatus.Sent, _ => executionOrder.Add("OnEntry"));
                });

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            StateMachine<Invoice, InvoiceStatus>.ForceTransition(invoice, InvoiceStatus.Sent);

            Assert.AreEqual(2, executionOrder.Count);
            Assert.AreEqual("OnExit", executionOrder[0]);
            Assert.AreEqual("OnEntry", executionOrder[1]);
        }

        #endregion

        #region TryTransition with Reason/Metadata Tests

        [TestMethod]
        public void TryTransition_WithReason_Succeeds()
        {
            ConfigureInvoiceStateMachine();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            var result = StateMachine<Invoice, InvoiceStatus>.TryTransition(
                invoice, InvoiceStatus.Sent, "Sending to customer");

            Assert.IsTrue(result);
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);
        }

        [TestMethod]
        public void TryTransition_WithMetadata_PassedToEvent()
        {
            IReadOnlyDictionary<string, object>? capturedMetadata = null;

            StateMachine<Invoice, InvoiceStatus>.Configure(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                });

            StateMachine<Invoice, InvoiceStatus>.OnTransition += ctx => capturedMetadata = ctx.Metadata;

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var metadata = new Dictionary<string, object>
            {
                ["Timestamp"] = DateTime.UtcNow,
                ["TriggeredBy"] = "System"
            };

            StateMachine<Invoice, InvoiceStatus>.TryTransition(invoice, InvoiceStatus.Sent, null, metadata);

            Assert.IsNotNull(capturedMetadata);
            Assert.AreEqual("System", capturedMetadata["TriggeredBy"]);
        }

        #endregion
    }
}