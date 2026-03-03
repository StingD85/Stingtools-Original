using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using StingBIM.Core.Logging;

namespace StingBIM.Core.Transactions
{
    /// <summary>
    /// Centralized transaction manager for Revit operations
    /// Provides automatic transaction handling, rollback on errors, and nested transaction support
    /// </summary>
    public sealed class TransactionManager
    {
        #region Private Fields
        
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<TransactionManager>();
        private readonly Document _document;
        private readonly Stack<Transaction> _transactionStack;
        private readonly Stack<TransactionGroup> _groupStack;
        private readonly object _transactionLock = new object();
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new TransactionManager for the specified document
        /// </summary>
        /// <param name="document">Revit document</param>
        public TransactionManager(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _transactionStack = new Stack<Transaction>();
            _groupStack = new Stack<TransactionGroup>();
            
            _logger.Debug($"TransactionManager created for document: {document.Title}");
        }
        
        #endregion

        #region Transaction Execution
        
        /// <summary>
        /// Executes an action within a transaction
        /// Automatically rolls back on exception
        /// </summary>
        /// <param name="transactionName">Name of the transaction</param>
        /// <param name="action">Action to execute</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool Execute(string transactionName, Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            
            using (_logger.StartPerformanceTimer($"Transaction: {transactionName}"))
            {
                Transaction transaction = null;
                
                try
                {
                    lock (_transactionLock)
                    {
                        transaction = new Transaction(_document, transactionName);
                        _transactionStack.Push(transaction);
                    }
                    
                    transaction.Start();
                    _logger.Debug($"Transaction started: {transactionName}");
                    
                    // Execute the action
                    action();
                    
                    // Commit if successful
                    TransactionStatus status = transaction.Commit();
                    
                    if (status == TransactionStatus.Committed)
                    {
                        _logger.Info($"Transaction committed: {transactionName}");
                        return true;
                    }
                    else
                    {
                        _logger.Warn($"Transaction status: {status} for {transactionName}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Transaction failed: {transactionName}");
                    
                    // Rollback on exception
                    if (transaction != null && transaction.HasStarted() && !transaction.HasEnded())
                    {
                        transaction.RollBack();
                        _logger.Info($"Transaction rolled back: {transactionName}");
                    }
                    
                    throw;
                }
                finally
                {
                    // Clean up transaction
                    lock (_transactionLock)
                    {
                        if (_transactionStack.Count > 0 && _transactionStack.Peek() == transaction)
                        {
                            _transactionStack.Pop();
                        }
                    }
                    
                    transaction?.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Executes a function within a transaction and returns the result
        /// Automatically rolls back on exception
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="transactionName">Name of the transaction</param>
        /// <param name="function">Function to execute</param>
        /// <returns>Result of the function</returns>
        public T Execute<T>(string transactionName, Func<T> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            
            T result = default;
            
            Execute(transactionName, () =>
            {
                result = function();
            });
            
            return result;
        }
        
        /// <summary>
        /// Executes an action safely, catching and logging exceptions
        /// Does not throw exceptions, returns success status
        /// </summary>
        /// <param name="transactionName">Name of the transaction</param>
        /// <param name="action">Action to execute</param>
        /// <returns>True if successful, false if exception occurred</returns>
        public bool ExecuteSafe(string transactionName, Action action)
        {
            try
            {
                return Execute(transactionName, action);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Safe transaction execution failed: {transactionName}");
                return false;
            }
        }
        
        #endregion

        #region Transaction Groups
        
        /// <summary>
        /// Executes multiple actions within a transaction group
        /// All transactions are committed together or rolled back together
        /// </summary>
        /// <param name="groupName">Name of the transaction group</param>
        /// <param name="action">Action containing multiple transactions</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ExecuteGroup(string groupName, Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            
            using (_logger.StartPerformanceTimer($"TransactionGroup: {groupName}"))
            {
                TransactionGroup group = null;
                
                try
                {
                    lock (_transactionLock)
                    {
                        group = new TransactionGroup(_document, groupName);
                        _groupStack.Push(group);
                    }
                    
                    group.Start();
                    _logger.Debug($"Transaction group started: {groupName}");
                    
                    // Execute the action (which may contain multiple transactions)
                    action();
                    
                    // Assimilate all transactions
                    TransactionStatus status = group.Assimilate();
                    
                    if (status == TransactionStatus.Committed)
                    {
                        _logger.Info($"Transaction group committed: {groupName}");
                        return true;
                    }
                    else
                    {
                        _logger.Warn($"Transaction group status: {status} for {groupName}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Transaction group failed: {groupName}");
                    
                    // Rollback on exception
                    if (group != null && group.HasStarted() && !group.HasEnded())
                    {
                        group.RollBack();
                        _logger.Info($"Transaction group rolled back: {groupName}");
                    }
                    
                    throw;
                }
                finally
                {
                    // Clean up group
                    lock (_transactionLock)
                    {
                        if (_groupStack.Count > 0 && _groupStack.Peek() == group)
                        {
                            _groupStack.Pop();
                        }
                    }
                    
                    group?.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Executes a transaction group safely, catching and logging exceptions
        /// Does not throw exceptions, returns success status
        /// </summary>
        /// <param name="groupName">Name of the transaction group</param>
        /// <param name="action">Action containing multiple transactions</param>
        /// <returns>True if successful, false if exception occurred</returns>
        public bool ExecuteGroupSafe(string groupName, Action action)
        {
            try
            {
                return ExecuteGroup(groupName, action);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Safe transaction group execution failed: {groupName}");
                return false;
            }
        }
        
        #endregion

        #region Sub-Transactions
        
        /// <summary>
        /// Executes an action within a sub-transaction
        /// Must be called within an active transaction
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ExecuteSubTransaction(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            
            // Verify we're in a transaction
            if (_transactionStack.Count == 0)
            {
                throw new InvalidOperationException("Sub-transaction can only be used within an active transaction");
            }
            
            using (_logger.StartPerformanceTimer("SubTransaction"))
            {
                SubTransaction subTransaction = null;
                
                try
                {
                    subTransaction = new SubTransaction(_document);
                    subTransaction.Start();
                    _logger.Debug("Sub-transaction started");
                    
                    // Execute the action
                    action();
                    
                    // Commit if successful
                    subTransaction.Commit();
                    _logger.Debug("Sub-transaction committed");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Sub-transaction failed");
                    
                    // Rollback on exception
                    if (subTransaction != null && subTransaction.HasStarted())
                    {
                        subTransaction.RollBack();
                        _logger.Debug("Sub-transaction rolled back");
                    }
                    
                    throw;
                }
                finally
                {
                    subTransaction?.Dispose();
                }
            }
        }
        
        #endregion

        #region Batch Transactions
        
        /// <summary>
        /// Executes multiple transactions in a batch with progress reporting
        /// </summary>
        /// <typeparam name="T">Item type</typeparam>
        /// <param name="groupName">Name of the batch group</param>
        /// <param name="items">Items to process</param>
        /// <param name="action">Action to execute for each item</param>
        /// <param name="batchSize">Number of items per transaction (default: 100)</param>
        /// <param name="progress">Progress reporter</param>
        /// <returns>Number of successfully processed items</returns>
        public int ExecuteBatch<T>(
            string groupName,
            IEnumerable<T> items,
            Action<T> action,
            int batchSize = 100,
            IProgress<int> progress = null)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (batchSize < 1)
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be at least 1");
            
            var itemList = items as IList<T> ?? new List<T>(items);
            int totalCount = itemList.Count;
            int processedCount = 0;
            int batchNumber = 0;
            
            _logger.Info($"Starting batch operation: {groupName} with {totalCount} items " +
                        $"(batch size: {batchSize})");
            
            try
            {
                return ExecuteGroup(groupName, () =>
                {
                    for (int i = 0; i < totalCount; i += batchSize)
                    {
                        batchNumber++;
                        int currentBatchSize = Math.Min(batchSize, totalCount - i);
                        
                        Execute($"{groupName} - Batch {batchNumber}", () =>
                        {
                            for (int j = 0; j < currentBatchSize; j++)
                            {
                                try
                                {
                                    action(itemList[i + j]);
                                    processedCount++;
                                    progress?.Report(processedCount);
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error(ex, $"Failed to process item {i + j} in batch {batchNumber}");
                                    throw; // This will rollback the current batch
                                }
                            }
                        });
                        
                        _logger.Debug($"Completed batch {batchNumber}/{((totalCount + batchSize - 1) / batchSize)} " +
                                    $"({processedCount}/{totalCount} items)");
                    }
                }) ? processedCount : 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Batch operation failed at batch {batchNumber}. " +
                                  $"Processed {processedCount}/{totalCount} items");
                throw;
            }
        }
        
        /// <summary>
        /// Executes batch operation safely, continuing on individual item failures
        /// </summary>
        /// <typeparam name="T">Item type</typeparam>
        /// <param name="groupName">Name of the batch group</param>
        /// <param name="items">Items to process</param>
        /// <param name="action">Action to execute for each item</param>
        /// <param name="batchSize">Number of items per transaction</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="failedItems">List to collect failed items</param>
        /// <returns>Number of successfully processed items</returns>
        public int ExecuteBatchSafe<T>(
            string groupName,
            IEnumerable<T> items,
            Action<T> action,
            int batchSize = 100,
            IProgress<int> progress = null,
            List<T> failedItems = null)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            
            var itemList = items as IList<T> ?? new List<T>(items);
            int successCount = 0;
            int failureCount = 0;
            
            _logger.Info($"Starting safe batch operation: {groupName} with {itemList.Count} items");
            
            foreach (var item in itemList)
            {
                try
                {
                    Execute($"{groupName} - Item", () => action(item));
                    successCount++;
                    progress?.Report(successCount + failureCount);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to process item in safe batch: {groupName}");
                    failedItems?.Add(item);
                    failureCount++;
                }
            }
            
            _logger.Info($"Safe batch operation completed: {successCount} succeeded, {failureCount} failed");
            return successCount;
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Checks if there is an active transaction
        /// </summary>
        /// <returns>True if transaction is active</returns>
        public bool IsTransactionActive()
        {
            lock (_transactionLock)
            {
                return _transactionStack.Count > 0;
            }
        }
        
        /// <summary>
        /// Checks if there is an active transaction group
        /// </summary>
        /// <returns>True if transaction group is active</returns>
        public bool IsGroupActive()
        {
            lock (_transactionLock)
            {
                return _groupStack.Count > 0;
            }
        }
        
        /// <summary>
        /// Gets the current transaction depth (for nested transactions)
        /// </summary>
        /// <returns>Number of active transactions</returns>
        public int GetTransactionDepth()
        {
            lock (_transactionLock)
            {
                return _transactionStack.Count;
            }
        }
        
        #endregion

        #region Static Factory Methods
        
        /// <summary>
        /// Creates a TransactionManager for the specified document
        /// </summary>
        /// <param name="document">Revit document</param>
        /// <returns>TransactionManager instance</returns>
        public static TransactionManager For(Document document)
        {
            return new TransactionManager(document);
        }
        
        #endregion
    }
}
