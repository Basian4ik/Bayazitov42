using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Bayazitov42
{
    /// <summary>
    /// Логика взаимодействия для OrderWindow.xaml
    /// </summary>
    public partial class OrderWindow : Window
    {
        private List<OrderProduct> selectedOrderProducts = new List<OrderProduct>();
        private List<Product> selectedProducts = new List<Product>();
        private Order currentOrder = new Order();
        private User _currentUser;
        public OrderWindow(List<OrderProduct> selectedOrderProducts, List<Product> selectedProducts, User user)
        {
            InitializeComponent();
            var currentPickups = Bayazitov41Entities.GetContext().PickupPoint.ToList();
            PickupPointComboBox.ItemsSource = currentPickups;
            if (user != null)
            {
                ClientNameText.Text = $"{user.UserSurname} {user.UserName} {user.UserPatronymic}";
            }
            else
            {
                ClientNameText.Text = "Гость";
            }

            _currentUser = user;

            // Устанавливаем товары
            this.selectedOrderProducts = selectedOrderProducts;
            this.selectedProducts = selectedProducts;
            // Устанавливаем количество для каждого товара
            foreach (Product p in selectedProducts)
            {
                p.Quantity = 1;
                foreach (OrderProduct q in selectedOrderProducts)
                {
                    if (p.ProductArticleNumber == q.ProductArticleNumber)
                        p.Quantity = q.Count;
                }
            }

            OrderItemsListView.ItemsSource = selectedProducts;
            DateTime orderDate = DateTime.Now;//дата
            // Номер заказа
            var lastOrder = Bayazitov41Entities.GetContext().Order.OrderByDescending(o => o.OrderID).FirstOrDefault();
            int newOrderNumber = lastOrder != null ? lastOrder.OrderID + 1 : 1;
            OrderNumberText.Text = newOrderNumber.ToString();
            SetDeliveryDate();
            UpdateTotals();
        }

        private void SetDeliveryDate()
        {
            // Проверяем наличие товаров
            bool fastDeliveryPossible = true;

            foreach (Product product in selectedProducts)
            {
                var orderProduct = selectedOrderProducts
                    .FirstOrDefault(op => op.ProductArticleNumber == product.ProductArticleNumber);

                if (orderProduct != null)
                {
                    if (product.ProductQuantityInStock == 0 || orderProduct.Count > 3)
                    {
                        fastDeliveryPossible = false;
                        break;
                    }
                }
                else
                {
                    fastDeliveryPossible = false;
                    break;
                }
            }

            int deliveryDays = fastDeliveryPossible ? 3 : 6;

            // ВАЖНО: OrderDateText - это TextBlock, у него нет SelectedDate!
            // Берем текущую дату
            DateTime orderDate = DateTime.Now;
            OrderDateText.Text = DateTime.Now.ToString("dd.MM.yyyy");
            // Или если вы храните дату в поле класса:
            // DateTime orderDate = _orderDate;

            var deliveryDate = orderDate.AddDays(deliveryDays);

            // Проверяем, что DeliveryDateText не null
            if (DeliveryDateText != null)
            {
                DeliveryDateText.Text = deliveryDate.ToString("dd.MM.yyyy");
            }
            else
            {
                // Альтернатива: выводим в MessageBox для отладки
                MessageBox.Show($"Дата доставки: {deliveryDate.ToString("dd.MM.yyyy")}");
            }
        }

        private void UpdateTotals()
        {
            decimal totalAmount = 0;
            decimal totalDiscount = 0;

            foreach (Product product in selectedProducts)
            {
                foreach (OrderProduct op in selectedOrderProducts)
                {
                    if (product.ProductArticleNumber == op.ProductArticleNumber)
                    {
                        int quantity = op.Count;
                        totalAmount += product.ProductCost * quantity;
                        totalDiscount += product.ProductCost * quantity * (product.ProductDiscountAmount / 100m);
                    }
                }
            }

            decimal finalAmount = totalAmount - totalDiscount;

        }






        private void OrderDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            SetDeliveryDate();
        }

        private void btnMinus_Click(object sender, RoutedEventArgs e)
        {
            var prod = (sender as Button).DataContext as Product;
            if (prod == null) return;

            if (prod.Quantity > 1)
            {
                prod.Quantity--;
                var selectedOP = selectedOrderProducts.FirstOrDefault(p => p.ProductArticleNumber == prod.ProductArticleNumber);
                if (selectedOP != null)
                {
                    int index = selectedOrderProducts.IndexOf(selectedOP);
                    selectedOrderProducts[index].Count--;
                }
                SetDeliveryDate();
                UpdateTotals();
                OrderItemsListView.Items.Refresh();
            }
        }

        private void btnPlus_Click(object sender, RoutedEventArgs e)
        {
            var prod = (sender as Button).DataContext as Product;
            if (prod == null) return;

            prod.Quantity++;

            var selectedOP = selectedOrderProducts.FirstOrDefault(p => p.ProductArticleNumber == prod.ProductArticleNumber);
            if (selectedOP != null)
            {
                int index = selectedOrderProducts.IndexOf(selectedOP);
                selectedOrderProducts[index].Count++;
            }

            SetDeliveryDate();
            UpdateTotals();
            OrderItemsListView.Items.Refresh();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Product product)
            {
                var result = MessageBox.Show("Удалить товар из заказа?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    selectedProducts.Remove(product);
                    var orderProduct = selectedOrderProducts.FirstOrDefault(op => op.ProductArticleNumber == product.ProductArticleNumber);
                    if (orderProduct != null)
                    {
                        selectedOrderProducts.Remove(orderProduct);
                    }

                    if (selectedProducts.Count == 0)
                    {
                        MessageBox.Show("Заказ пуст");

                        // ПРАВИЛЬНОЕ очищение:
                        // 1. Очищаем статический словарь
                        ProductPage.ClearOrderItems();

                        // 2. Устанавливаем специальный DialogResult
                        this.DialogResult = false;
                        this.Close();
                    }
                    else
                    {
                        OrderItemsListView.ItemsSource = null;
                        OrderItemsListView.ItemsSource = selectedProducts;
                        SetDeliveryDate();
                        UpdateTotals();
                    }
                }
            }
        }

        private void SaveOrderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Проверяем, есть ли товары
                if (selectedProducts.Count == 0)
                {
                    MessageBox.Show("Добавьте товары в заказ!");
                    return;
                }
                // 2. Проверяем пункт выдачи
                if (PickupPointComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Выберите пункт выдачи!");
                    return;
                }
                // 3. Получаем данные
                var selectedPoint = PickupPointComboBox.SelectedItem as PickupPoint;
                var context = Bayazitov41Entities.GetContext();
                // 4. Получаем дату доставки (с проверкой)
                //DateTime orderDate = DateTime.Now
                DateTime orderDate = DateTime.Now;
                DateTime deliveryDate;
                if (!DateTime.TryParse(DeliveryDateText.Text, out deliveryDate))
                {
                    deliveryDate = orderDate.AddDays(3);
                }
                // 5. Создаем заказ
                currentOrder = new Order
                {
                    OrderDate = orderDate,
                    OrderDeliveryDate = deliveryDate,
                    OrderPickupPoint = selectedPoint.PickupPointID,
                    // Не указываем OrderClientID здесь
                    OrderCode = new Random().Next(100000, 999999),
                    OrderStatus = "Новый"
                };
                // Правильная установка
                if (_currentUser != null)
                {
                    currentOrder.OrderClientID = _currentUser.UserID;
                }
                else
                {
                    currentOrder.OrderClientID = null;
                }
                // 6. Сохраняем заказ
                context.Order.Add(currentOrder);
                context.SaveChanges();
                // 7. Добавляем товары
                foreach (var op in selectedOrderProducts)
                {
                    op.OrderID = currentOrder.OrderID;
                    context.OrderProduct.Add(op);

                    // Обновляем остатки
                    var product = context.Product.FirstOrDefault(p => p.ProductArticleNumber == op.ProductArticleNumber);
                    if (product != null)
                    {
                        product.ProductQuantityInStock -= op.Count;
                    }
                }
                context.SaveChanges();

                // 8. Сообщаем об успехе
                MessageBox.Show($" Заказ №{currentOrder.OrderID} оформлен!\nКод: {currentOrder.OrderCode}",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // ПРАВИЛЬНОЕ очищение:
                // 1. Очищаем статический словарь
                ProductPage.ClearOrderItems();

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n\nДетали: {ex.InnerException?.Message}",
                    "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    public partial class Product
    {
        public int Quantity { get; set; }
        public decimal TotalPrice => ProductCost * Quantity;
    }

    public partial class PickupPoint
    {
        public string FullAddress => $"{PickupPointIndex} г. {PickupPointCity}, ул. {PickupPointStreet}, д. {PickupPointHome}";
    }
}

