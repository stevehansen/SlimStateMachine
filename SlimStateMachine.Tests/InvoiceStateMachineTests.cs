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
            Assert.IsTrue(graph.Contains("[*] --> Draft")); // Initial state
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
            Assert.IsTrue(graph.Contains("[*] --> Draft"));
            // Ensure Draft node exists even without transitions
            Assert.IsTrue(graph.TrimEnd().EndsWith("Draft") || graph.Contains("Draft\n")); // Might be just the initial link or a separate node def
        }

    }
}